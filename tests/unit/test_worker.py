"""
Phase 3.2 테스트: Worker 유닛 테스트
"""

import threading
import time

import pytest

from src.common.models import ExecutionConfig
from src.common.queue import TaskQueue
from src.worker import Worker


@pytest.fixture
def queue():
    """TaskQueue 픽스처"""
    try:
        queue = TaskQueue()
        # 테스트 전 큐 비우기 - queue_size를 확인하여 빈 큐에서 블로킹 방지
        while queue.get_queue_size() > 0:
            queue.get_task(timeout=1)
        yield queue
        queue.close()
    except Exception as e:
        pytest.skip(f"Redis not available: {e}")


@pytest.fixture
def worker(queue):
    """Worker 픽스처"""
    try:
        worker = Worker()
        yield worker
        worker.stop()
        worker.task_queue.close()
    except Exception as e:
        pytest.skip(f"Failed to create worker: {e}")


class TestWorker:
    """Worker 유닛 테스트"""

    def test_worker_creation(self, worker):
        """워커 생성 테스트"""
        assert worker is not None
        assert worker.task_queue is not None
        assert worker.running is False

    def test_worker_process_simple_task(self, queue, worker):
        """간단한 작업 처리 테스트"""
        # 작업 제출
        execution_id = queue.submit_task(
            code='print("Hello from worker!")', language="python"
        )

        # 워커를 백그라운드 스레드에서 실행
        def run_worker():
            worker.running = True
            task = worker.task_queue.get_task(timeout=2)
            if task:
                worker._process_task(task)
            worker.running = False

        worker_thread = threading.Thread(target=run_worker)
        worker_thread.start()
        worker_thread.join(timeout=10)

        # 결과 확인
        time.sleep(0.5)  # 결과 저장 대기
        status = queue.get_status(execution_id)

        assert status is not None
        assert status["status"] == "completed"
        assert "Hello from worker!" in status.get("stdout", "")
        assert status["exit_code"] == "0"

    def test_worker_process_error_task(self, queue, worker):
        """에러 발생 작업 처리 테스트"""
        # 에러 발생 코드 제출
        execution_id = queue.submit_task(code="x = 1 / 0", language="python")

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 결과 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)

        assert status["status"] == "failed"
        assert "ZeroDivisionError" in status.get("stderr", "")
        assert status["exit_code"] != "0"

    def test_worker_process_timeout_task(self, queue, worker):
        """타임아웃 작업 처리 테스트"""
        # 타임아웃 코드 제출
        execution_id = queue.submit_task(
            code="import time; time.sleep(10); print('done')",
            language="python",
            config=ExecutionConfig(timeout=2),
        )

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 결과 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)

        assert status["status"] == "failed"
        assert status["timeout"] == "True"
        assert "timeout" in status.get("stderr", "").lower()

    def test_worker_process_javascript_task(self, queue, worker):
        """JavaScript 작업 처리 테스트"""
        # JavaScript 코드 제출
        execution_id = queue.submit_task(
            code='console.log("Hello from JS!");', language="javascript"
        )

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 결과 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)

        assert status["status"] == "completed"
        assert "Hello from JS!" in status.get("stdout", "")
        assert status["exit_code"] == "0"

    def test_worker_updates_status(self, queue, worker):
        """워커가 상태를 올바르게 업데이트하는지 테스트"""
        # 작업 제출
        execution_id = queue.submit_task(
            code='print("status test")', language="python"
        )

        # 초기 상태 확인
        status = queue.get_status(execution_id)
        assert status["status"] == "queued"

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 최종 상태 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)
        assert status["status"] == "completed"
        assert "started_at" in status  # running 상태로 변경되었음
        assert "completed_at" in status

    def test_worker_handles_invalid_language(self, queue, worker):
        """지원하지 않는 언어 처리 테스트"""
        # 지원하지 않는 언어로 작업 제출
        execution_id = queue.submit_task(code='puts "hello"', language="ruby")

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 결과 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)

        assert status["status"] == "failed"
        assert "error" in status or "stderr" in status

    def test_worker_context_manager(self):
        """워커 컨텍스트 매니저 테스트"""
        try:
            with Worker() as worker:
                assert worker is not None
                assert worker.task_queue is not None
        except Exception as e:
            pytest.skip(f"Failed to create worker: {e}")

    def test_worker_multiple_tasks(self, queue, worker):
        """여러 작업 순차 처리 테스트"""
        # 여러 작업 제출
        id1 = queue.submit_task(code='print("task 1")', language="python")
        id2 = queue.submit_task(code='print("task 2")', language="python")
        id3 = queue.submit_task(code='print("task 3")', language="python")

        # 순차 처리
        for _ in range(3):
            task = queue.get_task(timeout=1)
            if task:
                worker._process_task(task)

        # 결과 확인
        time.sleep(1)
        status1 = queue.get_status(id1)
        status2 = queue.get_status(id2)
        status3 = queue.get_status(id3)

        assert status1["status"] == "completed"
        assert status2["status"] == "completed"
        assert status3["status"] == "completed"

    def test_worker_execution_duration(self, queue, worker):
        """실행 시간 측정 테스트"""
        # 작업 제출
        execution_id = queue.submit_task(
            code='import time; time.sleep(0.1); print("done")', language="python"
        )

        # 작업 처리
        task = queue.get_task(timeout=1)
        worker._process_task(task)

        # 결과 확인
        time.sleep(0.5)
        status = queue.get_status(execution_id)

        assert status["status"] == "completed"
        duration_ms = int(status.get("duration_ms", "0"))
        assert duration_ms >= 100  # 최소 100ms

"""
Phase 3.1 테스트: TaskQueue 유닛 테스트
"""

import time

import pytest

from src.common.models import ExecutionConfig
from src.common.queue import TaskQueue


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


class TestTaskQueue:
    """TaskQueue 유닛 테스트"""

    def test_submit_task(self, queue):
        """작업 제출 테스트"""
        execution_id = queue.submit_task(
            code='print("test")', language="python", config=ExecutionConfig()
        )

        assert execution_id is not None
        assert len(execution_id) == 36  # UUID 길이

        # 상태 확인
        status = queue.get_status(execution_id)
        assert status is not None
        assert status["status"] == "queued"
        assert status["language"] == "python"

    def test_get_task(self, queue):
        """작업 가져오기 테스트"""
        # 작업 제출
        code = 'console.log("hello");'
        execution_id = queue.submit_task(
            code=code, language="javascript", config=ExecutionConfig(timeout=10)
        )

        # 작업 가져오기
        task = queue.get_task(timeout=1)

        assert task is not None
        assert task["execution_id"] == execution_id
        assert task["code"] == code
        assert task["language"] == "javascript"
        assert task["timeout"] == 10

    def test_get_task_empty_queue(self, queue):
        """빈 큐에서 가져오기 테스트"""
        task = queue.get_task(timeout=1)
        assert task is None

    def test_update_status(self, queue):
        """상태 업데이트 테스트"""
        execution_id = queue.submit_task(
            code='print("test")', language="python", config=ExecutionConfig()
        )

        # 상태 업데이트
        queue.update_status(execution_id, status="running", started_at="2025-01-15T10:00:00Z")

        # 확인
        status = queue.get_status(execution_id)
        assert status["status"] == "running"
        assert status["started_at"] == "2025-01-15T10:00:00Z"

    def test_save_result(self, queue):
        """결과 저장 테스트"""
        execution_id = queue.submit_task(
            code='print("test")', language="python", config=ExecutionConfig()
        )

        # 결과 저장
        queue.save_result(
            execution_id,
            stdout="test output",
            stderr="",
            exit_code=0,
            duration_ms=123,
            timeout=False,
        )

        # 확인
        status = queue.get_status(execution_id)
        assert status["status"] == "completed"
        assert status["stdout"] == "test output"
        assert status["stderr"] == ""
        assert status["exit_code"] == "0"
        assert status["duration_ms"] == "123"
        assert status["timeout"] == "False"

    def test_save_result_with_error(self, queue):
        """에러 결과 저장 테스트"""
        execution_id = queue.submit_task(
            code="x = 1/0", language="python", config=ExecutionConfig()
        )

        # 에러 결과 저장
        queue.save_result(
            execution_id,
            stdout="",
            stderr="ZeroDivisionError: division by zero",
            exit_code=1,
            duration_ms=50,
            timeout=False,
            error_type="ZeroDivisionError",
        )

        # 확인
        status = queue.get_status(execution_id)
        assert status["status"] == "failed"
        assert "ZeroDivisionError" in status["stderr"]
        assert status["exit_code"] == "1"
        assert status["error_type"] == "ZeroDivisionError"

    def test_save_result_with_timeout(self, queue):
        """타임아웃 결과 저장 테스트"""
        execution_id = queue.submit_task(
            code="while True: pass", language="python", config=ExecutionConfig(timeout=2)
        )

        # 타임아웃 결과 저장
        queue.save_result(
            execution_id,
            stdout="",
            stderr="Execution timeout after 2 seconds",
            exit_code=-1,
            duration_ms=2000,
            timeout=True,
            error_type="TimeoutError",
        )

        # 확인
        status = queue.get_status(execution_id)
        assert status["status"] == "failed"
        assert status["timeout"] == "True"
        assert status["error_type"] == "TimeoutError"

    def test_get_status_not_found(self, queue):
        """존재하지 않는 실행 ID 조회 테스트"""
        status = queue.get_status("non-existent-id")
        assert status is None

    def test_get_queue_size(self, queue):
        """큐 크기 조회 테스트"""
        initial_size = queue.get_queue_size()

        # 3개 작업 제출
        queue.submit_task(code='print("1")', language="python")
        queue.submit_task(code='print("2")', language="python")
        queue.submit_task(code='print("3")', language="python")

        assert queue.get_queue_size() == initial_size + 3

        # 1개 가져오기
        queue.get_task(timeout=1)
        assert queue.get_queue_size() == initial_size + 2

    def test_multiple_tasks_fifo(self, queue):
        """FIFO 순서 테스트"""
        # 순서대로 제출
        id1 = queue.submit_task(code='print("first")', language="python")
        id2 = queue.submit_task(code='print("second")', language="python")
        id3 = queue.submit_task(code='print("third")', language="python")

        # 순서대로 가져오기
        task1 = queue.get_task(timeout=1)
        task2 = queue.get_task(timeout=1)
        task3 = queue.get_task(timeout=1)

        assert task1["execution_id"] == id1
        assert task2["execution_id"] == id2
        assert task3["execution_id"] == id3

    def test_context_manager(self):
        """컨텍스트 매니저 테스트"""
        try:
            with TaskQueue() as queue:
                execution_id = queue.submit_task(
                    code='print("test")', language="python"
                )
                assert execution_id is not None
        except Exception as e:
            pytest.skip(f"Redis not available: {e}")

    def test_execution_config_serialization(self, queue):
        """ExecutionConfig 직렬화 테스트"""
        config = ExecutionConfig(
            timeout=15, memory_limit="512m", cpu_limit=1.0, network_enabled=True
        )

        execution_id = queue.submit_task(
            code='print("test")', language="python", config=config
        )

        task = queue.get_task(timeout=1)

        assert task["timeout"] == 15
        assert task["memory_limit"] == "512m"
        assert task["cpu_limit"] == 1.0
        assert task["network_enabled"] is True

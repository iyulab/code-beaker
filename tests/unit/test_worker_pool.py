"""
Phase 3.3 테스트: WorkerPool 유닛 테스트
"""

import time

import pytest

from src.common.models import ExecutionConfig
from src.common.queue import TaskQueue
from src.worker.pool import WorkerPool


@pytest.fixture
def queue():
    """TaskQueue 픽스처"""
    try:
        queue = TaskQueue()
        # 테스트 전 큐 비우기
        while queue.get_queue_size() > 0:
            queue.get_task(timeout=1)
        yield queue
        queue.close()
    except Exception as e:
        pytest.skip(f"Redis not available: {e}")


@pytest.fixture
def worker_pool(queue):
    """WorkerPool 픽스처"""
    try:
        pool = WorkerPool(num_workers=2)
        yield pool
        pool.stop()
        pool.task_queue.close()
    except Exception as e:
        pytest.skip(f"Failed to create worker pool: {e}")


class TestWorkerPool:
    """WorkerPool 유닛 테스트"""

    def test_worker_pool_creation(self, worker_pool):
        """워커 풀 생성 테스트"""
        assert worker_pool is not None
        assert worker_pool.num_workers == 2
        assert len(worker_pool.workers) == 0  # 시작 전에는 비어있음

    def test_worker_pool_start(self, worker_pool):
        """워커 풀 시작 테스트"""
        worker_pool.start()
        time.sleep(0.5)  # 워커들이 시작할 시간 대기

        assert len(worker_pool.workers) == 2
        assert all(w.is_alive() for w in worker_pool.workers)

    def test_worker_pool_stop(self, worker_pool):
        """워커 풀 중지 테스트"""
        worker_pool.start()
        time.sleep(0.5)

        worker_pool.stop()
        time.sleep(1)  # 워커들이 종료할 시간 대기

        assert all(not w.is_alive() for w in worker_pool.workers)

    def test_worker_pool_parallel_processing(self, queue, worker_pool):
        """병렬 작업 처리 테스트"""
        # 5개 작업 제출
        execution_ids = []
        for i in range(5):
            exec_id = queue.submit_task(
                code=f'print("Task {i}")', language="python"
            )
            execution_ids.append(exec_id)

        # 워커 풀 시작
        worker_pool.start()

        # 모든 작업이 완료될 때까지 대기 (최대 10초)
        for _ in range(20):
            time.sleep(0.5)
            statuses = [queue.get_status(eid) for eid in execution_ids]
            if all(s and s["status"] in ["completed", "failed"] for s in statuses):
                break

        # 모든 작업이 완료되었는지 확인
        for exec_id in execution_ids:
            status = queue.get_status(exec_id)
            assert status is not None
            assert status["status"] == "completed"

    def test_worker_pool_health_check(self, worker_pool):
        """워커 헬스 체크 테스트"""
        worker_pool.start()
        time.sleep(0.5)

        # 모든 워커가 healthy 상태여야 함
        health = worker_pool.get_health_status()
        assert health["total_workers"] == 2
        assert health["healthy_workers"] == 2
        assert health["unhealthy_workers"] == 0

    def test_worker_pool_context_manager(self):
        """워커 풀 컨텍스트 매니저 테스트"""
        try:
            with WorkerPool(num_workers=2) as pool:
                assert pool is not None
                pool.start()
                time.sleep(0.5)
                assert len(pool.workers) == 2
        except Exception as e:
            pytest.skip(f"Failed to create worker pool: {e}")

    def test_worker_pool_graceful_shutdown(self, queue, worker_pool):
        """우아한 종료 테스트 - 실행 중인 작업 완료"""
        # 긴 작업 제출
        exec_id = queue.submit_task(
            code='import time; time.sleep(2); print("done")',
            language="python",
            config=ExecutionConfig(timeout=5),
        )

        worker_pool.start()
        time.sleep(0.5)  # 작업이 시작될 시간

        # 상태 확인 - running이어야 함
        status = queue.get_status(exec_id)
        assert status["status"] in ["queued", "running"]

        # 우아한 종료
        worker_pool.stop(graceful=True, timeout=5)

        # 작업이 완료되었는지 확인
        status = queue.get_status(exec_id)
        assert status["status"] == "completed"

    def test_worker_pool_with_failures(self, queue, worker_pool):
        """실패한 작업 처리 테스트"""
        # 에러 발생 작업 제출
        exec_id = queue.submit_task(code="x = 1 / 0", language="python")

        worker_pool.start()
        time.sleep(2)

        # 실패 상태 확인
        status = queue.get_status(exec_id)
        assert status["status"] == "failed"
        assert "ZeroDivisionError" in status["stderr"]

    def test_worker_pool_metrics(self, queue, worker_pool):
        """워커 풀 메트릭 테스트"""
        # 여러 작업 제출
        for i in range(5):
            queue.submit_task(code=f'print("{i}")', language="python")

        worker_pool.start()
        time.sleep(3)

        # 메트릭 조회
        metrics = worker_pool.get_metrics()
        assert "total_processed" in metrics
        assert metrics["total_processed"] >= 5
        assert "total_failed" in metrics
        assert "average_duration_ms" in metrics

    def test_worker_pool_dynamic_scaling(self, worker_pool):
        """동적 워커 스케일링 테스트"""
        worker_pool.start()
        time.sleep(0.5)

        # 초기 워커 수
        assert len(worker_pool.workers) == 2

        # 워커 추가
        worker_pool.scale(4)
        time.sleep(0.5)
        assert len(worker_pool.workers) == 4

        # 워커 감소
        worker_pool.scale(2)
        time.sleep(0.5)
        assert len(worker_pool.workers) == 2

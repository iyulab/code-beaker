"""
파일시스템 기반 아키텍처 통합 테스트

FileQueue와 FileStorage를 사용한 전체 워크플로우 테스트
"""

import shutil
import time
from pathlib import Path

import pytest

from src.common.file_queue import FileQueue
from src.common.file_storage import FileStorage
from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry


@pytest.fixture
def test_data_dir(tmp_path):
    """테스트용 임시 데이터 디렉토리"""
    data_dir = tmp_path / "data"
    yield data_dir
    # 테스트 후 정리
    if data_dir.exists():
        shutil.rmtree(data_dir)


@pytest.fixture
def file_queue(test_data_dir):
    """파일 큐 fixture"""
    queue_dir = test_data_dir / "queue"
    return FileQueue(str(queue_dir))


@pytest.fixture
def file_storage(test_data_dir):
    """파일 저장소 fixture"""
    exec_dir = test_data_dir / "executions"
    return FileStorage(str(exec_dir))


class TestFileQueue:
    """FileQueue 테스트"""

    def test_submit_and_get_task(self, file_queue):
        """작업 제출 및 가져오기"""
        # 작업 제출
        exec_id = file_queue.submit_task(
            'print("Hello")', "python", ExecutionConfig(timeout=5)
        )

        assert exec_id is not None
        assert len(exec_id) == 36  # UUID 길이

        # 큐 크기 확인
        assert file_queue.get_queue_size() == 1

        # 작업 가져오기
        task = file_queue.get_task(timeout=1)

        assert task is not None
        assert task["execution_id"] == exec_id
        assert task["code"] == 'print("Hello")'
        assert task["language"] == "python"
        assert task["timeout"] == 5

        # 큐가 비어있어야 함
        assert file_queue.get_queue_size() == 0

    def test_get_task_timeout(self, file_queue):
        """타임아웃 테스트"""
        # 빈 큐에서 가져오기
        start = time.time()
        task = file_queue.get_task(timeout=1)
        elapsed = time.time() - start

        assert task is None
        assert elapsed >= 1.0  # 최소 1초 대기

    def test_multiple_tasks_fifo(self, file_queue):
        """여러 작업 FIFO 순서 확인"""
        # 3개 작업 제출
        ids = []
        for i in range(3):
            exec_id = file_queue.submit_task(f'print({i})', "python", ExecutionConfig())
            ids.append(exec_id)
            time.sleep(0.01)  # 타임스탬프 구분을 위해

        assert file_queue.get_queue_size() == 3

        # FIFO 순서로 가져오기
        for expected_id in ids:
            task = file_queue.get_task(timeout=1)
            assert task["execution_id"] == expected_id

        assert file_queue.get_queue_size() == 0

    def test_concurrent_workers(self, file_queue):
        """동시 워커 테스트 (경합 조건)"""
        # 5개 작업 제출
        for i in range(5):
            file_queue.submit_task(f'print({i})', "python", ExecutionConfig())

        # 2개 워커가 작업 가져가기
        worker1_tasks = []
        worker2_tasks = []

        for _ in range(3):
            task = file_queue.get_task(timeout=1)
            if task:
                worker1_tasks.append(task["execution_id"])

        for _ in range(2):
            task = file_queue.get_task(timeout=1)
            if task:
                worker2_tasks.append(task["execution_id"])

        # 중복 없이 5개 작업 처리
        all_tasks = set(worker1_tasks + worker2_tasks)
        assert len(all_tasks) == 5
        assert file_queue.get_queue_size() == 0


class TestFileStorage:
    """FileStorage 테스트"""

    def test_update_and_get_status(self, file_storage):
        """상태 저장 및 조회"""
        exec_id = "test-execution-123"

        # 상태 업데이트
        file_storage.update_status(
            exec_id, "queued", language="python", created_at="2025-01-01T00:00:00Z"
        )

        # 상태 조회
        status = file_storage.get_status(exec_id)

        assert status is not None
        assert status["execution_id"] == exec_id
        assert status["status"] == "queued"
        assert status["language"] == "python"
        assert "updated_at" in status

        # 상태 업데이트
        file_storage.update_status(exec_id, "running")

        status = file_storage.get_status(exec_id)
        assert status["status"] == "running"

    def test_save_and_get_result(self, file_storage):
        """실행 결과 저장 및 조회"""
        exec_id = "test-result-456"

        # 결과 저장
        file_storage.save_result(
            exec_id,
            stdout="Hello World\n",
            stderr="",
            exit_code=0,
            duration_ms=123,
            timeout=False,
        )

        # 결과 조회
        result = file_storage.get_result(exec_id)

        assert result is not None
        assert result["execution_id"] == exec_id
        assert result["status"] == "completed"
        assert result["stdout"] == "Hello World\n"
        assert result["stderr"] == ""
        assert result["exit_code"] == 0
        assert result["duration_ms"] == 123
        assert result["timeout"] is False

    def test_nonexistent_execution(self, file_storage):
        """존재하지 않는 실행 조회"""
        status = file_storage.get_status("nonexistent-id")
        assert status is None

        result = file_storage.get_result("nonexistent-id")
        assert result is None


class TestEndToEndWorkflow:
    """전체 워크플로우 통합 테스트"""

    def test_python_execution_workflow(self, file_queue, file_storage):
        """Python 코드 전체 실행 워크플로우"""
        # 1. 작업 제출
        code = 'print("Hello from Python")'
        exec_id = file_queue.submit_task(code, "python", ExecutionConfig(timeout=10))

        # 2. 작업 가져오기
        task = file_queue.get_task(timeout=1)
        assert task is not None

        # 3. 상태 업데이트: running
        file_storage.update_status(exec_id, "running")

        # 4. 코드 실행
        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(timeout=task["timeout"])
        result = runtime.execute(task["code"], config)

        # 5. 결과 저장
        file_queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
        )

        # 6. 결과 검증
        saved_result = file_storage.get_result(exec_id)
        assert saved_result["status"] == "completed"
        assert saved_result["exit_code"] == 0
        assert "Hello from Python" in saved_result["stdout"]

    def test_javascript_execution_workflow(self, file_queue, file_storage):
        """JavaScript 코드 전체 실행 워크플로우"""
        code = 'console.log("Hello from JavaScript")'
        exec_id = file_queue.submit_task(code, "javascript", ExecutionConfig(timeout=10))

        task = file_queue.get_task(timeout=1)
        file_storage.update_status(exec_id, "running")

        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(timeout=task["timeout"])
        result = runtime.execute(task["code"], config)

        file_queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
        )

        saved_result = file_storage.get_result(exec_id)
        assert saved_result["status"] == "completed"
        assert saved_result["exit_code"] == 0
        assert "Hello from JavaScript" in saved_result["stdout"]

    @pytest.mark.skipif(
        not Path("docker/runtimes/csharp/Dockerfile").exists(),
        reason="C# runtime not available",
    )
    def test_csharp_execution_workflow(self, file_queue, file_storage):
        """C# 코드 전체 실행 워크플로우"""
        code = """
using System;
class Program
{
    static void Main()
    {
        Console.WriteLine("Hello from C#");
    }
}
"""
        exec_id = file_queue.submit_task(code, "csharp", ExecutionConfig(timeout=20))

        task = file_queue.get_task(timeout=1)
        file_storage.update_status(exec_id, "running")

        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(timeout=task["timeout"])
        result = runtime.execute(task["code"], config)

        file_queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
        )

        saved_result = file_storage.get_result(exec_id)
        assert saved_result["status"] == "completed"
        assert saved_result["exit_code"] == 0
        assert "Hello from C#" in saved_result["stdout"]

    def test_error_handling_workflow(self, file_queue, file_storage):
        """에러 처리 워크플로우"""
        # 의도적인 에러 코드
        code = 'raise Exception("Test error")'
        exec_id = file_queue.submit_task(code, "python", ExecutionConfig(timeout=10))

        task = file_queue.get_task(timeout=1)
        file_storage.update_status(exec_id, "running")

        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(timeout=task["timeout"])
        result = runtime.execute(task["code"], config)

        file_queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
        )

        saved_result = file_storage.get_result(exec_id)
        assert saved_result["status"] == "failed"
        assert saved_result["exit_code"] != 0
        assert "Test error" in saved_result["stderr"]

    def test_timeout_workflow(self, file_queue, file_storage):
        """타임아웃 처리 워크플로우"""
        # 무한 루프 코드
        code = "import time\nwhile True: time.sleep(0.1)"
        exec_id = file_queue.submit_task(code, "python", ExecutionConfig(timeout=2))

        task = file_queue.get_task(timeout=1)
        file_storage.update_status(exec_id, "running")

        runtime = RuntimeRegistry.get(task["language"])
        config = ExecutionConfig(timeout=task["timeout"])
        result = runtime.execute(task["code"], config)

        file_queue.save_result(
            exec_id,
            result.stdout,
            result.stderr,
            result.exit_code,
            result.duration_ms,
            result.timeout,
        )

        saved_result = file_storage.get_result(exec_id)
        assert saved_result["status"] == "failed"
        assert saved_result["timeout"] is True or saved_result["timeout"] == "True"


class TestMetrics:
    """메트릭 테스트"""

    def test_metrics_file_creation(self, file_queue, file_storage, test_data_dir):
        """메트릭 파일 생성 확인"""
        # 작업 실행
        exec_id = file_queue.submit_task('print("test")', "python", ExecutionConfig())
        task = file_queue.get_task(timeout=1)

        runtime = RuntimeRegistry.get(task["language"])
        result = runtime.execute(task["code"], ExecutionConfig())

        file_queue.save_result(
            exec_id, result.stdout, result.stderr, result.exit_code, result.duration_ms
        )

        # 메트릭 파일 확인
        metrics_file = test_data_dir / "metrics" / "counters.json"
        assert metrics_file.exists()

        # 메트릭 내용 확인
        import json

        with open(metrics_file, "r") as f:
            metrics = json.load(f)

        assert "total_processed" in metrics
        assert metrics["total_processed"] >= 1

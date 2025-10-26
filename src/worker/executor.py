"""
Phase 3.2: 워커 프로세스

큐에서 작업을 가져와 실행하는 워커
"""

import sys
import time
from datetime import UTC, datetime

from src.common.models import ExecutionConfig
from src.common.queue import TaskQueue
from src.runtime import RuntimeRegistry


class Worker:
    """작업 큐 워커"""

    def __init__(self, redis_url: str = "redis://localhost:6379/0"):
        """
        Args:
            redis_url: Redis 연결 URL
        """
        self.task_queue = TaskQueue(redis_url)
        self.running = False

    def start(self):
        """워커 시작"""
        self.running = True
        print(f"Worker started at {datetime.now(UTC).isoformat()}")
        print("Waiting for tasks...")

        while self.running:
            try:
                # 큐에서 작업 가져오기 (블로킹, 1초 타임아웃)
                task = self.task_queue.get_task(timeout=1)

                if not task:
                    continue  # 큐가 비어있음

                self._process_task(task)

            except KeyboardInterrupt:
                print("\nReceived shutdown signal...")
                self.running = False
                break

            except Exception as e:
                print(f"Error in worker loop: {e}")
                time.sleep(1)

        print("Worker stopped")

    def _process_task(self, task: dict):
        """
        작업 처리

        Args:
            task: 작업 데이터
        """
        execution_id = task["execution_id"]
        language = task["language"]
        code = task["code"]

        print(f"[{execution_id}] Processing {language} code...")

        try:
            # 상태 업데이트: queued -> running
            self.task_queue.update_status(
                execution_id, status="running", started_at=datetime.now(UTC).isoformat()
            )

            # 실행 설정
            config = ExecutionConfig(
                timeout=task.get("timeout", 5),
                memory_limit=task.get("memory_limit", "256m"),
                cpu_limit=task.get("cpu_limit", 0.5),
                network_enabled=task.get("network_enabled", False),
            )

            # 런타임 가져오기
            runtime = RuntimeRegistry.get(language)

            # 코드 실행
            result = runtime.execute(code, config)

            # 결과 저장
            self.task_queue.save_result(
                execution_id,
                stdout=result.stdout,
                stderr=result.stderr,
                exit_code=result.exit_code,
                duration_ms=result.duration_ms or 0,
                timeout=result.timeout,
                error_type=result.error_type,
            )

            status = "completed" if result.exit_code == 0 and not result.timeout else "failed"
            print(f"[{execution_id}] {status.upper()} in {result.duration_ms}ms")

        except Exception as e:
            # 실행 실패
            print(f"[{execution_id}] FAILED: {e}")
            self.task_queue.update_status(
                execution_id,
                status="failed",
                error=str(e),
                stderr=str(e),
                failed_at=datetime.now(UTC).isoformat(),
            )

    def stop(self):
        """워커 중지"""
        self.running = False

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.stop()
        self.task_queue.close()


def main():
    """워커 실행"""
    try:
        worker = Worker()
        worker.start()
    except Exception as e:
        print(f"Failed to start worker: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

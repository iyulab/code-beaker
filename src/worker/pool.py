"""
Phase 3.3: 워커 풀 관리

여러 워커 프로세스를 관리하는 워커 풀
"""

import multiprocessing
import time
from datetime import UTC, datetime
from typing import Dict, List

from src.common.queue import TaskQueue
from src.worker.executor import Worker


def _run_worker_process(worker_id: int, redis_url: str):
    """
    워커 프로세스 실행 함수 (모듈 레벨 함수)

    Args:
        worker_id: 워커 ID
        redis_url: Redis 연결 URL
    """
    try:
        worker = Worker(redis_url)
        worker.start()
    except KeyboardInterrupt:
        pass
    except Exception as e:
        print(f"Worker-{worker_id} failed: {e}")


class WorkerPool:
    """워커 풀 - 여러 워커 프로세스 관리"""

    def __init__(
        self, num_workers: int = 2, redis_url: str = "redis://localhost:6379/0"
    ):
        """
        Args:
            num_workers: 워커 프로세스 수
            redis_url: Redis 연결 URL
        """
        self.num_workers = num_workers
        self.redis_url = redis_url
        self.task_queue = TaskQueue(redis_url)
        self.workers: List[multiprocessing.Process] = []
        self.running = False

    def start(self):
        """워커 풀 시작"""
        if self.running:
            return

        self.running = True
        print(f"Starting worker pool with {self.num_workers} workers...")

        # 워커 프로세스 생성 및 시작
        for i in range(self.num_workers):
            worker_process = multiprocessing.Process(
                target=_run_worker_process,
                args=(i, self.redis_url),
                name=f"Worker-{i}",
            )
            worker_process.start()
            self.workers.append(worker_process)
            print(f"Worker-{i} started (PID: {worker_process.pid})")

        print(f"Worker pool started with {len(self.workers)} workers")

    def stop(self, graceful: bool = True, timeout: int = 5):
        """
        워커 풀 중지

        Args:
            graceful: 우아한 종료 (실행 중인 작업 완료 대기)
            timeout: 종료 대기 시간 (초)
        """
        if not self.running:
            return

        print(f"Stopping worker pool ({'graceful' if graceful else 'forced'})...")
        self.running = False

        if graceful:
            # 우아한 종료: 워커들이 종료할 때까지 대기
            for worker in self.workers:
                worker.join(timeout=timeout)
                if worker.is_alive():
                    print(f"Force terminating {worker.name}")
                    worker.terminate()
        else:
            # 강제 종료
            for worker in self.workers:
                worker.terminate()

        # 모든 프로세스 정리
        for worker in self.workers:
            worker.join(timeout=1)

        self.workers.clear()
        print("Worker pool stopped")

    def scale(self, num_workers: int):
        """
        워커 수 동적 조정

        Args:
            num_workers: 목표 워커 수
        """
        current_count = len(self.workers)

        if num_workers > current_count:
            # 워커 추가
            for i in range(current_count, num_workers):
                worker_process = multiprocessing.Process(
                    target=_run_worker_process,
                    args=(i, self.redis_url),
                    name=f"Worker-{i}",
                )
                worker_process.start()
                self.workers.append(worker_process)
                print(f"Added Worker-{i} (PID: {worker_process.pid})")

        elif num_workers < current_count:
            # 워커 제거
            workers_to_remove = self.workers[num_workers:]
            self.workers = self.workers[:num_workers]

            for worker in workers_to_remove:
                worker.terminate()
                worker.join(timeout=1)
                print(f"Removed {worker.name}")

        self.num_workers = num_workers

    def get_health_status(self) -> Dict:
        """
        워커 풀 헬스 상태 조회

        Returns:
            헬스 상태 정보
        """
        healthy = sum(1 for w in self.workers if w.is_alive())
        unhealthy = len(self.workers) - healthy

        return {
            "total_workers": len(self.workers),
            "healthy_workers": healthy,
            "unhealthy_workers": unhealthy,
            "timestamp": datetime.now(UTC).isoformat(),
        }

    def get_metrics(self) -> Dict:
        """
        워커 풀 메트릭 조회

        Returns:
            메트릭 정보
        """
        # Redis에서 메트릭 조회
        total_processed = int(
            self.task_queue.redis_client.get(
                f"{self.task_queue.METRICS_PREFIX}total_processed"
            )
            or 0
        )
        total_failed = int(
            self.task_queue.redis_client.get(
                f"{self.task_queue.METRICS_PREFIX}total_failed"
            )
            or 0
        )
        total_duration_ms = int(
            self.task_queue.redis_client.get(
                f"{self.task_queue.METRICS_PREFIX}total_duration_ms"
            )
            or 0
        )

        avg_duration = (
            total_duration_ms / total_processed if total_processed > 0 else 0
        )

        return {
            "total_processed": total_processed,
            "total_failed": total_failed,
            "average_duration_ms": int(avg_duration),
            "queue_size": self.task_queue.get_queue_size(),
            "workers": len(self.workers),
            "timestamp": datetime.now(UTC).isoformat(),
        }

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.stop()
        self.task_queue.close()

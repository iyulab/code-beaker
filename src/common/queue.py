"""
Phase 3.1: 작업 큐 관리

Redis를 사용한 작업 큐 및 상태 관리
"""

import json
import uuid
from datetime import UTC, datetime
from typing import Optional

import redis

from src.common.models import ExecutionConfig


class TaskQueue:
    """Redis 기반 작업 큐"""

    QUEUE_KEY = "codebeaker:queue"
    EXECUTION_PREFIX = "execution:"
    METRICS_PREFIX = "codebeaker:metrics:"

    def __init__(self, redis_url: str = "redis://localhost:6379/0"):
        """
        Args:
            redis_url: Redis 연결 URL
        """
        self.redis_client = redis.from_url(redis_url, decode_responses=True)

    def submit_task(
        self,
        code: str,
        language: str,
        config: ExecutionConfig = ExecutionConfig(),
    ) -> str:
        """
        작업을 큐에 제출

        Args:
            code: 실행할 코드
            language: 프로그래밍 언어
            config: 실행 설정

        Returns:
            실행 ID
        """
        # 고유 실행 ID 생성
        execution_id = str(uuid.uuid4())

        # 작업 데이터 직렬화
        task = {
            "execution_id": execution_id,
            "code": code,
            "language": language,
            "timeout": config.timeout,
            "memory_limit": config.memory_limit,
            "cpu_limit": config.cpu_limit,
            "network_enabled": config.network_enabled,
            "created_at": datetime.now(UTC).isoformat(),
        }

        # Redis 큐에 삽입
        self.redis_client.rpush(self.QUEUE_KEY, json.dumps(task))

        # 초기 상태 저장
        self.redis_client.hset(
            f"{self.EXECUTION_PREFIX}{execution_id}",
            mapping={
                "status": "queued",
                "language": language,
                "created_at": task["created_at"],
            },
        )

        # 24시간 후 자동 삭제 설정
        self.redis_client.expire(f"{self.EXECUTION_PREFIX}{execution_id}", 86400)

        return execution_id

    def get_task(self, timeout: int = 1) -> Optional[dict]:
        """
        큐에서 작업 가져오기 (블로킹)

        Args:
            timeout: 대기 시간 (초)

        Returns:
            작업 데이터 또는 None
        """
        task_data = self.redis_client.blpop(self.QUEUE_KEY, timeout=timeout)

        if not task_data:
            return None

        queue_name, task_json = task_data
        return json.loads(task_json)

    def update_status(
        self,
        execution_id: str,
        status: str,
        **kwargs,
    ) -> None:
        """
        실행 상태 업데이트

        Args:
            execution_id: 실행 ID
            status: 상태 (queued, running, completed, failed)
            **kwargs: 추가 필드
        """
        update_data = {"status": status, "updated_at": datetime.now(UTC).isoformat()}
        update_data.update(kwargs)

        self.redis_client.hset(
            f"{self.EXECUTION_PREFIX}{execution_id}", mapping=update_data
        )

    def get_status(self, execution_id: str) -> Optional[dict]:
        """
        실행 상태 조회

        Args:
            execution_id: 실행 ID

        Returns:
            상태 정보 또는 None
        """
        data = self.redis_client.hgetall(f"{self.EXECUTION_PREFIX}{execution_id}")

        if not data:
            return None

        return data

    def save_result(
        self,
        execution_id: str,
        stdout: str,
        stderr: str,
        exit_code: int,
        duration_ms: int,
        timeout: bool = False,
        error_type: Optional[str] = None,
    ) -> None:
        """
        실행 결과 저장

        Args:
            execution_id: 실행 ID
            stdout: 표준 출력
            stderr: 표준 에러
            exit_code: 종료 코드
            duration_ms: 실행 시간 (ms)
            timeout: 타임아웃 여부
            error_type: 에러 타입
        """
        status = "completed" if exit_code == 0 and not timeout else "failed"

        self.update_status(
            execution_id,
            status=status,
            stdout=stdout,
            stderr=stderr,
            exit_code=str(exit_code),
            duration_ms=str(duration_ms),
            timeout=str(timeout),
            error_type=error_type or "",
            completed_at=datetime.now(UTC).isoformat(),
        )

        # 메트릭 업데이트
        self.redis_client.incr(f"{self.METRICS_PREFIX}total_processed")
        if status == "failed":
            self.redis_client.incr(f"{self.METRICS_PREFIX}total_failed")
        self.redis_client.incrby(f"{self.METRICS_PREFIX}total_duration_ms", duration_ms)

    def get_queue_size(self) -> int:
        """큐에 대기 중인 작업 수 조회"""
        return self.redis_client.llen(self.QUEUE_KEY)

    def close(self) -> None:
        """Redis 연결 종료"""
        self.redis_client.close()

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.close()

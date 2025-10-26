"""
파일시스템 기반 작업 큐

Redis 대신 파일시스템을 사용한 작업 큐 구현
"""

import json
import os
import time
import uuid
from datetime import UTC, datetime
from pathlib import Path
from typing import Optional

from src.common.models import ExecutionConfig


class FileQueue:
    """파일시스템 기반 작업 큐"""

    def __init__(self, base_dir: str = "data/queue"):
        """
        Args:
            base_dir: 큐 데이터 저장 디렉토리
        """
        self.base_dir = Path(base_dir)
        self.pending_dir = self.base_dir / "pending"
        self.processing_dir = self.base_dir / "processing"

        # 디렉토리 생성
        self.pending_dir.mkdir(parents=True, exist_ok=True)
        self.processing_dir.mkdir(parents=True, exist_ok=True)

        # 데이터 루트 디렉토리 (queue의 부모: data/)
        self.data_root = self.base_dir.parent

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

        # 작업 데이터
        task = {
            "execution_id": execution_id,
            "code": code,
            "language": language,
            "timeout": config.timeout,
            "memory_limit": config.memory_limit,
            "cpu_limit": config.cpu_limit,
            "network_enabled": config.network_enabled,
            "packages": config.packages,
            "created_at": datetime.now(UTC).isoformat(),
        }

        # 타임스탬프 기반 파일명 (정렬 용이)
        timestamp = datetime.now(UTC).strftime("%Y%m%d_%H%M%S_%f")
        filename = f"{timestamp}_{execution_id}.json"

        # 원자적 쓰기 (임시 파일 → rename)
        temp_file = self.pending_dir / f".tmp_{filename}"
        target_file = self.pending_dir / filename

        with open(temp_file, "w", encoding="utf-8") as f:
            json.dump(task, f, indent=2)

        # 원자적 이동 (Windows에서는 기존 파일이 없어야 함)
        if target_file.exists():
            target_file.unlink()
        temp_file.rename(target_file)

        return execution_id

    def get_task(self, timeout: int = 1) -> Optional[dict]:
        """
        큐에서 작업 가져오기

        Args:
            timeout: 대기 시간 (초) - 파일 큐에서는 폴링

        Returns:
            작업 데이터 또는 None
        """
        start_time = time.time()

        while True:
            # pending 디렉토리에서 가장 오래된 작업 찾기
            try:
                task_files = sorted(self.pending_dir.glob("*.json"))
                if not task_files:
                    # 타임아웃 체크
                    if time.time() - start_time >= timeout:
                        return None
                    time.sleep(0.1)  # 100ms 대기 후 재시도
                    continue

                task_file = task_files[0]

                # processing 디렉토리로 이동 (원자적)
                processing_file = self.processing_dir / task_file.name

                try:
                    # Windows에서는 기존 파일이 있으면 삭제
                    if processing_file.exists():
                        processing_file.unlink()

                    # 이동 (다른 워커와 경합 발생 가능)
                    task_file.rename(processing_file)

                except (FileNotFoundError, OSError):
                    # 다른 워커가 가져간 경우, 다음 파일 시도
                    continue

                # 파일 읽기
                with open(processing_file, "r", encoding="utf-8") as f:
                    task = json.load(f)

                return task

            except Exception:
                # 에러 발생 시 재시도
                if time.time() - start_time >= timeout:
                    return None
                time.sleep(0.1)
                continue

    def update_status(
        self,
        execution_id: str,
        status: str,
        **kwargs,
    ) -> None:
        """
        실행 상태 업데이트 (FileStorage로 위임)

        Args:
            execution_id: 실행 ID
            status: 상태
            **kwargs: 추가 필드
        """
        # FileStorage에서 처리 (같은 data root 사용)
        from src.common.file_storage import FileStorage

        executions_dir = self.data_root / "executions"
        storage = FileStorage(str(executions_dir))
        storage.update_status(execution_id, status, **kwargs)

    def get_status(self, execution_id: str) -> Optional[dict]:
        """
        실행 상태 조회 (FileStorage로 위임)

        Args:
            execution_id: 실행 ID

        Returns:
            상태 정보 또는 None
        """
        from src.common.file_storage import FileStorage

        executions_dir = self.data_root / "executions"
        storage = FileStorage(str(executions_dir))
        return storage.get_status(execution_id)

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
        실행 결과 저장 (FileStorage로 위임)

        Args:
            execution_id: 실행 ID
            stdout: 표준 출력
            stderr: 표준 에러
            exit_code: 종료 코드
            duration_ms: 실행 시간 (ms)
            timeout: 타임아웃 여부
            error_type: 에러 타입
        """
        from src.common.file_storage import FileStorage

        executions_dir = self.data_root / "executions"
        storage = FileStorage(str(executions_dir))
        storage.save_result(
            execution_id, stdout, stderr, exit_code, duration_ms, timeout, error_type
        )

        # processing에서 작업 파일 삭제
        for task_file in self.processing_dir.glob(f"*_{execution_id}.json"):
            try:
                task_file.unlink()
            except FileNotFoundError:
                pass

    def get_queue_size(self) -> int:
        """큐에 대기 중인 작업 수 조회"""
        return len(list(self.pending_dir.glob("*.json")))

    def close(self) -> None:
        """리소스 정리 (파일 큐는 불필요)"""
        pass

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.close()

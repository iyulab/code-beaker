"""
파일시스템 기반 상태 저장소

Redis 대신 파일시스템을 사용한 실행 결과 및 상태 저장
"""

import json
import threading
from datetime import UTC, datetime
from pathlib import Path
from typing import Optional


class FileStorage:
    """파일시스템 기반 상태 저장소"""

    def __init__(self, base_dir: str = "data/executions"):
        """
        Args:
            base_dir: 실행 데이터 저장 디렉토리
        """
        self.base_dir = Path(base_dir)
        self.base_dir.mkdir(parents=True, exist_ok=True)

    def _get_execution_dir(self, execution_id: str) -> Path:
        """실행 ID에 대한 디렉토리 경로"""
        return self.base_dir / execution_id

    def _ensure_execution_dir(self, execution_id: str) -> Path:
        """실행 디렉토리 생성"""
        exec_dir = self._get_execution_dir(execution_id)
        exec_dir.mkdir(parents=True, exist_ok=True)
        return exec_dir

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
        exec_dir = self._ensure_execution_dir(execution_id)
        status_file = exec_dir / "status.json"

        # 기존 상태 읽기 (있으면)
        if status_file.exists():
            with open(status_file, "r", encoding="utf-8") as f:
                data = json.load(f)
        else:
            data = {"execution_id": execution_id}

        # 상태 업데이트
        data["status"] = status
        data["updated_at"] = datetime.now(UTC).isoformat()
        data.update(kwargs)

        # 원자적 쓰기
        temp_file = exec_dir / ".tmp_status.json"
        with open(temp_file, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)

        # Windows에서 기존 파일 삭제 후 이동
        if status_file.exists():
            status_file.unlink()
        temp_file.rename(status_file)

    def get_status(self, execution_id: str) -> Optional[dict]:
        """
        실행 상태 조회

        Args:
            execution_id: 실행 ID

        Returns:
            상태 정보 또는 None
        """
        exec_dir = self._get_execution_dir(execution_id)
        status_file = exec_dir / "status.json"

        if not status_file.exists():
            return None

        with open(status_file, "r", encoding="utf-8") as f:
            return json.load(f)

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
        exec_dir = self._ensure_execution_dir(execution_id)

        # stdout 저장
        stdout_file = exec_dir / "stdout.txt"
        with open(stdout_file, "w", encoding="utf-8") as f:
            f.write(stdout)

        # stderr 저장
        stderr_file = exec_dir / "stderr.txt"
        with open(stderr_file, "w", encoding="utf-8") as f:
            f.write(stderr)

        # 상태 업데이트
        status = "completed" if exit_code == 0 and not timeout else "failed"
        self.update_status(
            execution_id,
            status=status,
            exit_code=exit_code,
            duration_ms=duration_ms,
            timeout=timeout,
            error_type=error_type or "",
            completed_at=datetime.now(UTC).isoformat(),
        )

        # 메트릭 업데이트
        self._update_metrics(status, duration_ms)

    def _update_metrics(self, status: str, duration_ms: int) -> None:
        """
        메트릭 업데이트 (파일 잠금 사용)

        Args:
            status: 실행 상태
            duration_ms: 실행 시간 (ms)
        """
        # base_dir의 부모에 metrics 디렉토리 생성 (data/executions -> data/metrics)
        metrics_dir = self.base_dir.parent / "metrics"
        metrics_dir.mkdir(parents=True, exist_ok=True)
        counters_file = metrics_dir / "counters.json"

        # 파일 잠금을 위한 스레드 락
        lock = threading.Lock()

        with lock:
            # 기존 메트릭 읽기
            if counters_file.exists():
                with open(counters_file, "r", encoding="utf-8") as f:
                    counters = json.load(f)
            else:
                counters = {
                    "total_processed": 0,
                    "total_failed": 0,
                    "total_duration_ms": 0,
                }

            # 메트릭 업데이트
            counters["total_processed"] = counters.get("total_processed", 0) + 1
            if status == "failed":
                counters["total_failed"] = counters.get("total_failed", 0) + 1
            counters["total_duration_ms"] = (
                counters.get("total_duration_ms", 0) + duration_ms
            )

            # 원자적 쓰기
            temp_file = metrics_dir / ".tmp_counters.json"
            with open(temp_file, "w", encoding="utf-8") as f:
                json.dump(counters, f, indent=2)

            if counters_file.exists():
                counters_file.unlink()
            temp_file.rename(counters_file)

    def get_result(self, execution_id: str) -> Optional[dict]:
        """
        실행 결과 조회 (상태 + stdout + stderr)

        Args:
            execution_id: 실행 ID

        Returns:
            실행 결과 또는 None
        """
        exec_dir = self._get_execution_dir(execution_id)

        # 상태 조회
        status = self.get_status(execution_id)
        if not status:
            return None

        # stdout 읽기
        stdout_file = exec_dir / "stdout.txt"
        if stdout_file.exists():
            with open(stdout_file, "r", encoding="utf-8") as f:
                status["stdout"] = f.read()
        else:
            status["stdout"] = ""

        # stderr 읽기
        stderr_file = exec_dir / "stderr.txt"
        if stderr_file.exists():
            with open(stderr_file, "r", encoding="utf-8") as f:
                status["stderr"] = f.read()
        else:
            status["stderr"] = ""

        return status

    def close(self) -> None:
        """리소스 정리 (파일 저장소는 불필요)"""
        pass

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.close()

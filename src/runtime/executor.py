"""
Phase 1.1: Hello World 실행기

가장 단순한 Python 코드 실행기 구현
"""

import os
import subprocess
import tempfile
import time
from pathlib import Path
from typing import Dict

from src.common.models import ExecutionConfig, ExecutionResult


class SimpleExecutor:
    """간단한 코드 실행기 (subprocess 기반)"""

    def execute_python(
        self, code: str, config: ExecutionConfig = ExecutionConfig()
    ) -> ExecutionResult:
        """
        Python 코드를 실행하고 결과를 반환

        Args:
            code: 실행할 Python 코드
            config: 실행 설정

        Returns:
            ExecutionResult: 실행 결과
        """
        # 임시 파일에 코드 작성
        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".py", delete=False, encoding="utf-8"
        ) as f:
            f.write(code)
            temp_path = f.name

        start_time = time.time()
        timeout_occurred = False

        try:
            # Python 실행
            result = subprocess.run(
                ["python", temp_path],
                capture_output=True,
                text=True,
                timeout=config.timeout,
            )

            duration_ms = int((time.time() - start_time) * 1000)

            return ExecutionResult(
                stdout=result.stdout,
                stderr=result.stderr,
                exit_code=result.returncode,
                duration_ms=duration_ms,
                timeout=False,
            )

        except subprocess.TimeoutExpired:
            duration_ms = int((time.time() - start_time) * 1000)
            timeout_occurred = True

            return ExecutionResult(
                stdout="",
                stderr=f"Execution timeout after {config.timeout} seconds",
                exit_code=-1,
                duration_ms=duration_ms,
                timeout=True,
                error_type="TimeoutError",
            )

        except Exception as e:
            duration_ms = int((time.time() - start_time) * 1000)

            return ExecutionResult(
                stdout="",
                stderr=str(e),
                exit_code=-1,
                duration_ms=duration_ms,
                error_type=type(e).__name__,
            )

        finally:
            # 임시 파일 삭제
            try:
                os.unlink(temp_path)
            except Exception:
                pass  # 삭제 실패해도 무시


def execute_python_simple(code: str, timeout: int = 5) -> Dict[str, any]:
    """
    가장 단순한 형태의 Python 실행 함수 (레거시 인터페이스)

    Args:
        code: 실행할 Python 코드
        timeout: 타임아웃 (초)

    Returns:
        dict: {'stdout': str, 'stderr': str, 'exit_code': int}
    """
    executor = SimpleExecutor()
    config = ExecutionConfig(timeout=timeout)
    result = executor.execute_python(code, config)

    return {
        "stdout": result.stdout,
        "stderr": result.stderr,
        "exit_code": result.exit_code,
        "duration_ms": result.duration_ms,
        "timeout": result.timeout,
    }

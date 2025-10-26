"""
Phase 1.4: Docker 컨테이너 기반 실행기

Docker SDK를 사용하여 격리된 컨테이너에서 코드 실행
"""

import os
import tempfile
import time
from pathlib import Path
from typing import Optional

import docker
from docker.errors import ContainerError, ImageNotFound, APIError

from src.common.models import ExecutionConfig, ExecutionResult


class DockerExecutor:
    """Docker 컨테이너 기반 코드 실행기"""

    def __init__(self, image: str = "codebeaker-python:latest"):
        """
        Args:
            image: 사용할 Docker 이미지 이름
        """
        self.image = image
        try:
            self.client = docker.from_env()
            # 이미지 존재 확인
            self.client.images.get(self.image)
        except ImageNotFound:
            raise RuntimeError(
                f"Docker image '{self.image}' not found. "
                f"Please build it first with: "
                f"docker build -t {self.image} docker/runtimes/python/"
            )
        except APIError as e:
            raise RuntimeError(f"Docker API error: {e}")

    def execute_python(
        self,
        code: str,
        config: ExecutionConfig = ExecutionConfig(),
    ) -> ExecutionResult:
        """
        Python 코드를 Docker 컨테이너에서 실행

        Args:
            code: 실행할 Python 코드
            config: 실행 설정

        Returns:
            ExecutionResult: 실행 결과
        """
        # 임시 디렉토리에 코드 작성
        with tempfile.TemporaryDirectory() as tmpdir:
            code_path = Path(tmpdir) / "code.py"
            code_path.write_text(code, encoding="utf-8")

            start_time = time.time()

            try:
                # Docker 컨테이너 생성 및 시작
                container = self.client.containers.create(
                    image=self.image,
                    command=["python", "/workspace/code.py"],
                    volumes={tmpdir: {"bind": "/workspace", "mode": "ro"}},
                    # 리소스 제한
                    mem_limit=config.memory_limit,
                    nano_cpus=int(config.cpu_limit * 1e9),
                    # 보안 옵션
                    network_disabled=not config.network_enabled,
                    # 실행 옵션
                    detach=True,
                )

                try:
                    # 컨테이너 시작
                    container.start()

                    # 타임아웃과 함께 대기
                    exit_status = container.wait(timeout=config.timeout)

                    # 로그 가져오기
                    stdout = container.logs(stdout=True, stderr=False)
                    stderr = container.logs(stdout=False, stderr=True)

                    duration_ms = int((time.time() - start_time) * 1000)

                    # exit_status는 딕셔너리 형태: {'StatusCode': 0}
                    exit_code = exit_status.get("StatusCode", -1)

                    return ExecutionResult(
                        stdout=stdout.decode("utf-8") if stdout else "",
                        stderr=stderr.decode("utf-8") if stderr else "",
                        exit_code=exit_code,
                        duration_ms=duration_ms,
                        timeout=False,
                    )

                except Exception as wait_error:
                    # 타임아웃 또는 실행 에러
                    duration_ms = int((time.time() - start_time) * 1000)

                    # 컨테이너 강제 종료
                    try:
                        container.kill()
                    except Exception:
                        pass

                    # 타임아웃 체크
                    if "timed out" in str(wait_error).lower() or "timeout" in str(wait_error).lower():
                        return ExecutionResult(
                            stdout="",
                            stderr=f"Execution timeout after {config.timeout} seconds",
                            exit_code=-1,
                            duration_ms=duration_ms,
                            timeout=True,
                            error_type="TimeoutError",
                        )

                    # 기타 에러
                    return ExecutionResult(
                        stdout="",
                        stderr=str(wait_error),
                        exit_code=-1,
                        duration_ms=duration_ms,
                        error_type=type(wait_error).__name__,
                    )

                finally:
                    # 컨테이너 제거
                    try:
                        container.remove(force=True)
                    except Exception:
                        pass

            except Exception as e:
                # 컨테이너 생성 실패
                duration_ms = int((time.time() - start_time) * 1000)

                return ExecutionResult(
                    stdout="",
                    stderr=f"Failed to create container: {str(e)}",
                    exit_code=-1,
                    duration_ms=duration_ms,
                    error_type=type(e).__name__,
                )

    def cleanup(self) -> None:
        """Docker 클라이언트 정리"""
        if hasattr(self, "client"):
            self.client.close()

    def __enter__(self):
        """컨텍스트 매니저 진입"""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """컨텍스트 매니저 종료"""
        self.cleanup()

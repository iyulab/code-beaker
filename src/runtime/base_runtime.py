"""
Phase 2.1: 런타임 추상화 - 기본 인터페이스

모든 언어 런타임의 공통 인터페이스 정의
"""

import time
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Optional

import docker
from docker.errors import ContainerError

from src.common.models import ExecutionConfig, ExecutionResult


class BaseRuntime(ABC):
    """모든 언어 런타임의 기본 인터페이스"""

    def __init__(self):
        """Docker 클라이언트 초기화"""
        try:
            self.client = docker.from_env()
            # 이미지 존재 확인
            image = self.get_docker_image()
            try:
                self.client.images.get(image)
            except docker.errors.ImageNotFound:
                raise RuntimeError(
                    f"Docker image '{image}' not found. "
                    f"Please build or pull it first."
                )
        except docker.errors.APIError as e:
            raise RuntimeError(f"Docker API error: {e}")

    @abstractmethod
    def get_docker_image(self) -> str:
        """
        사용할 Docker 이미지 이름 반환

        Returns:
            Docker 이미지 이름 (예: "python:3.11-slim")
        """
        pass

    @abstractmethod
    def get_language_name(self) -> str:
        """
        언어 이름 반환

        Returns:
            언어 이름 (예: "python", "javascript", "csharp")
        """
        pass

    @abstractmethod
    def prepare_code(
        self, code: str, workspace_dir: Path, packages: list[str] = None
    ) -> str:
        """
        코드를 파일로 저장하고 컨테이너 내 엔트리 포인트 경로 반환

        Args:
            code: 실행할 소스 코드
            workspace_dir: 호스트의 임시 작업 디렉토리 (볼륨 마운트될 위치)
            packages: 설치할 패키지 목록

        Returns:
            컨테이너 내부의 엔트리 포인트 경로 (예: "/workspace/code.py")
        """
        pass

    @abstractmethod
    def get_run_command(self, entry_point: str, packages: list[str] = None) -> list[str]:
        """
        실행 명령어 생성

        Args:
            entry_point: 컨테이너 내부의 엔트리 포인트 경로
            packages: 설치할 패키지 목록

        Returns:
            실행 명령어 리스트 (예: ["python", "/workspace/code.py"])
        """
        pass

    def execute(
        self, code: str, config: ExecutionConfig = ExecutionConfig()
    ) -> ExecutionResult:
        """
        코드를 Docker 컨테이너에서 실행 (템플릿 메서드 패턴)

        모든 런타임이 공유하는 실행 로직. 하위 클래스는 추상 메서드만 구현.

        Args:
            code: 실행할 코드
            config: 실행 설정

        Returns:
            ExecutionResult: 실행 결과
        """
        import tempfile

        # 임시 디렉토리에 코드 준비
        with tempfile.TemporaryDirectory() as tmpdir:
            workspace_dir = Path(tmpdir)

            # 언어별 코드 준비 (추상 메서드)
            entry_point = self.prepare_code(code, workspace_dir, config.packages)

            # 실행 명령어 생성 (추상 메서드)
            command = self.get_run_command(entry_point, config.packages)

            start_time = time.time()

            try:
                # Docker 컨테이너 생성 및 시작
                container = self.client.containers.create(
                    image=self.get_docker_image(),
                    command=command,
                    volumes={str(tmpdir): {"bind": "/workspace", "mode": "ro"}},
                    # 리소스 제한
                    mem_limit=config.memory_limit,
                    nano_cpus=int(config.cpu_limit * 1e9),
                    # 보안 옵션
                    network_disabled=not config.network_enabled,
                    read_only=True,  # 루트 파일시스템 읽기 전용
                    tmpfs={"/tmp": "size=512m,mode=1777,exec"},  # tmpfs (패키지 설치용, exec 허용)
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
                    if (
                        "timed out" in str(wait_error).lower()
                        or "timeout" in str(wait_error).lower()
                    ):
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

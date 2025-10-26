"""
Phase 2.1: Python 런타임 구현
Phase 2.4: 패키지 설치 지원

BaseRuntime을 상속하여 Python 코드 실행 구현
"""

from pathlib import Path

from src.common.packages import validate_packages
from src.runtime.base_runtime import BaseRuntime


class PythonRuntime(BaseRuntime):
    """Python 코드 실행 런타임"""

    def __init__(self, image: str = "codebeaker-python:latest"):
        """
        Args:
            image: 사용할 Docker 이미지 이름
        """
        self.image = image
        super().__init__()

    def get_docker_image(self) -> str:
        """Python Docker 이미지 반환"""
        return self.image

    def get_language_name(self) -> str:
        """언어 이름 반환"""
        return "python"

    def validate_packages_internal(self, packages: list[str]) -> None:
        """
        패키지 화이트리스트 검증

        Args:
            packages: 설치할 패키지 목록

        Raises:
            ValueError: 허용되지 않은 패키지가 포함된 경우
        """
        if not packages:
            return

        is_valid, invalid = validate_packages(packages, "python")
        if not is_valid:
            raise ValueError(f"Packages not allowed: {', '.join(invalid)}")

    def prepare_code(
        self, code: str, workspace_dir: Path, packages: list[str] = None
    ) -> str:
        """
        Python 코드를 파일로 저장

        Args:
            code: Python 소스 코드
            workspace_dir: 호스트의 임시 작업 디렉토리
            packages: 설치할 패키지 목록

        Returns:
            컨테이너 내부의 파일 경로
        """
        # 패키지 검증
        if packages:
            self.validate_packages_internal(packages)

            # requirements.txt 생성
            req_path = workspace_dir / "requirements.txt"
            req_path.write_text("\n".join(packages), encoding="utf-8")

        # 코드 파일 저장
        code_path = workspace_dir / "code.py"
        code_path.write_text(code, encoding="utf-8")
        return "/workspace/code.py"

    def get_run_command(self, entry_point: str, packages: list[str] = None) -> list[str]:
        """
        Python 실행 명령어 생성

        Args:
            entry_point: 컨테이너 내부의 파일 경로
            packages: 설치할 패키지 목록

        Returns:
            실행 명령어
        """
        if packages:
            # 패키지 설치 + 코드 실행
            # tmpfs(/tmp)에 설치하고 PYTHONPATH 설정
            return [
                "sh",
                "-c",
                f"pip install --no-cache-dir -q --target /tmp/packages -r /workspace/requirements.txt && PYTHONPATH=/tmp/packages python {entry_point}",
            ]
        else:
            # 코드만 실행
            return ["python", entry_point]

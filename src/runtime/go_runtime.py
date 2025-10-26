"""
Go 런타임 구현

BaseRuntime을 상속하여 Go 코드 실행 구현
"""

from pathlib import Path

from src.runtime.base_runtime import BaseRuntime


class GoRuntime(BaseRuntime):
    """Go 코드 실행 런타임"""

    def __init__(self, image: str = "codebeaker-golang:latest"):
        """
        Args:
            image: 사용할 Docker 이미지 이름
        """
        self.image = image
        super().__init__()

    def get_docker_image(self) -> str:
        """Go Docker 이미지 반환"""
        return self.image

    def get_language_name(self) -> str:
        """언어 이름 반환"""
        return "go"

    def prepare_code(
        self, code: str, workspace_dir: Path, packages: list[str] = None
    ) -> str:
        """
        Go 코드를 파일로 저장

        Args:
            code: Go 소스 코드
            workspace_dir: 호스트의 임시 작업 디렉토리
            packages: 설치할 패키지 목록 (Go 모듈)

        Returns:
            컨테이너 내부의 파일 경로
        """
        # go.mod 파일 생성 (패키지 의존성)
        if packages:
            go_mod_content = "module main\n\ngo 1.21\n\nrequire (\n"
            for pkg in packages:
                go_mod_content += f"\t{pkg} latest\n"
            go_mod_content += ")\n"

            go_mod_path = workspace_dir / "go.mod"
            go_mod_path.write_text(go_mod_content, encoding="utf-8")

        # 코드 파일 저장
        code_path = workspace_dir / "main.go"
        code_path.write_text(code, encoding="utf-8")
        return "/workspace/main.go"

    def get_run_command(self, entry_point: str, packages: list[str] = None) -> list[str]:
        """
        Go 실행 명령어 생성

        Args:
            entry_point: 컨테이너 내부의 파일 경로
            packages: 설치할 패키지 목록

        Returns:
            실행 명령어
        """
        if packages:
            # 패키지 다운로드 + 빌드 + 실행
            # tmpfs에 빌드하여 read-only 파일시스템 우회
            # GOCACHE를 /tmp로 설정하여 캐시도 tmpfs에 저장
            return [
                "sh",
                "-c",
                "export GOCACHE=/tmp/.cache && export GOMODCACHE=/tmp/.modcache && "
                "mkdir -p /tmp/build && cp -r /workspace/* /tmp/build/ && cd /tmp/build && "
                "go mod download && go build -o /tmp/app main.go && /tmp/app",
            ]
        else:
            # 빌드 없이 바로 실행 (간단한 코드)
            # tmpfs에 빌드하여 read-only 파일시스템 우회
            # GOCACHE를 /tmp로 설정하여 캐시도 tmpfs에 저장
            return [
                "sh",
                "-c",
                "export GOCACHE=/tmp/.cache && export GOMODCACHE=/tmp/.modcache && "
                "mkdir -p /tmp/build && cp /workspace/main.go /tmp/build/ && cd /tmp/build && "
                "go build -o /tmp/app main.go && /tmp/app",
            ]

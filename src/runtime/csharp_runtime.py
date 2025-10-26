"""
Phase 2.3: C# 런타임 구현

BaseRuntime을 상속하여 C# 코드 실행 구현
"""

from pathlib import Path

from src.runtime.base_runtime import BaseRuntime


class CSharpRuntime(BaseRuntime):
    """C# 코드 실행 런타임"""

    def __init__(self, image: str = "codebeaker-csharp:latest"):
        """
        Args:
            image: 사용할 Docker 이미지 이름
        """
        self.image = image
        super().__init__()

    def get_docker_image(self) -> str:
        """.NET Docker 이미지 반환"""
        return self.image

    def get_language_name(self) -> str:
        """언어 이름 반환"""
        return "csharp"

    def prepare_code(
        self, code: str, workspace_dir: Path, packages: list[str] = None
    ) -> str:
        """
        C# 코드를 파일로 저장하고 프로젝트 파일 생성

        Args:
            code: C# 소스 코드
            workspace_dir: 호스트의 임시 작업 디렉토리
            packages: 설치할 패키지 목록 (Phase 2.3에서는 미지원)

        Returns:
            컨테이너 내부의 프로젝트 경로
        """
        # .csproj 파일 생성 (프로젝트 설정)
        csproj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
"""
        csproj_path = workspace_dir / "Program.csproj"
        csproj_path.write_text(csproj_content, encoding="utf-8")

        # Program.cs 파일 생성
        code_path = workspace_dir / "Program.cs"
        code_path.write_text(code, encoding="utf-8")

        # 프로젝트 디렉토리 경로 반환
        return "/workspace"

    def get_run_command(self, entry_point: str, packages: list[str] = None) -> list[str]:
        """
        C# 실행 명령어 생성 (컴파일 + 실행)

        Args:
            entry_point: 컨테이너 내부의 프로젝트 경로
            packages: 설치할 패키지 목록 (Phase 2.3에서는 미지원)

        Returns:
            실행 명령어
        """
        # dotnet build는 obj/, bin/ 디렉토리를 생성하므로
        # read-only /workspace 대신 writable /tmp에서 빌드
        # 1. /tmp/build 디렉토리 생성
        # 2. 프로젝트 파일을 /tmp/build로 복사
        # 3. /tmp/build에서 빌드 및 실행
        # 4. .NET CLI 환경 변수를 tmpfs로 설정
        return [
            "sh",
            "-c",
            "mkdir -p /tmp/build && cp -r /workspace/* /tmp/build/ && cd /tmp/build && "
            "DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 "
            "dotnet run",
        ]

"""
Phase 2.1: 런타임 레지스트리

언어별 런타임 인스턴스를 관리하는 레지스트리 패턴
"""

from typing import Dict

from src.runtime.base_runtime import BaseRuntime


class RuntimeRegistry:
    """런타임 레지스트리 - 싱글톤 패턴"""

    _runtimes: Dict[str, BaseRuntime] = {}

    @classmethod
    def register(cls, language: str, runtime: BaseRuntime) -> None:
        """
        런타임 등록

        Args:
            language: 언어 식별자 (예: "python", "javascript")
            runtime: 런타임 인스턴스
        """
        cls._runtimes[language.lower()] = runtime

    @classmethod
    def get(cls, language: str) -> BaseRuntime:
        """
        런타임 조회

        Args:
            language: 언어 식별자

        Returns:
            BaseRuntime: 해당 언어의 런타임 인스턴스

        Raises:
            ValueError: 지원하지 않는 언어인 경우
        """
        language_key = language.lower()
        if language_key not in cls._runtimes:
            supported = ", ".join(cls._runtimes.keys())
            raise ValueError(
                f"Unsupported language: {language}. "
                f"Supported languages: {supported}"
            )
        return cls._runtimes[language_key]

    @classmethod
    def list_languages(cls) -> list[str]:
        """
        지원하는 모든 언어 목록 반환

        Returns:
            언어 식별자 리스트
        """
        return list(cls._runtimes.keys())

    @classmethod
    def is_supported(cls, language: str) -> bool:
        """
        언어 지원 여부 확인

        Args:
            language: 언어 식별자

        Returns:
            지원 여부
        """
        return language.lower() in cls._runtimes

    @classmethod
    def clear(cls) -> None:
        """모든 등록된 런타임 제거 (테스트용)"""
        cls._runtimes.clear()


# 기본 런타임 등록
def initialize_default_runtimes():
    """기본 제공 런타임 초기화 및 등록"""
    import warnings

    # Python 런타임 등록
    try:
        from src.runtime.python_runtime import PythonRuntime

        python_runtime = PythonRuntime()
        RuntimeRegistry.register("python", python_runtime)
        RuntimeRegistry.register("py", python_runtime)  # 별칭

    except RuntimeError as e:
        # Docker 이미지가 없는 경우 경고만 출력
        warnings.warn(f"Failed to initialize Python runtime: {e}")

    # JavaScript 런타임 등록
    try:
        from src.runtime.javascript_runtime import JavaScriptRuntime

        javascript_runtime = JavaScriptRuntime()
        RuntimeRegistry.register("javascript", javascript_runtime)
        RuntimeRegistry.register("js", javascript_runtime)  # 별칭
        RuntimeRegistry.register("nodejs", javascript_runtime)  # 별칭
        RuntimeRegistry.register("node", javascript_runtime)  # 별칭

    except RuntimeError as e:
        # Docker 이미지가 없는 경우 경고만 출력
        warnings.warn(f"Failed to initialize JavaScript runtime: {e}")

    # C# 런타임 등록
    try:
        from src.runtime.csharp_runtime import CSharpRuntime

        csharp_runtime = CSharpRuntime()
        RuntimeRegistry.register("csharp", csharp_runtime)
        RuntimeRegistry.register("cs", csharp_runtime)  # 별칭
        RuntimeRegistry.register("dotnet", csharp_runtime)  # 별칭

    except RuntimeError as e:
        # Docker 이미지가 없는 경우 경고만 출력
        warnings.warn(f"Failed to initialize C# runtime: {e}")

    # Go 런타임 등록
    try:
        from src.runtime.go_runtime import GoRuntime

        go_runtime = GoRuntime()
        RuntimeRegistry.register("go", go_runtime)
        RuntimeRegistry.register("golang", go_runtime)  # 별칭

    except RuntimeError as e:
        # Docker 이미지가 없는 경우 경고만 출력
        warnings.warn(f"Failed to initialize Go runtime: {e}")

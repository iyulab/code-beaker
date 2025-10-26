"""런타임 계층 - 언어별 실행 어댑터"""

from src.runtime.base_runtime import BaseRuntime
from src.runtime.csharp_runtime import CSharpRuntime
from src.runtime.docker_executor import DockerExecutor
from src.runtime.executor import SimpleExecutor
from src.runtime.javascript_runtime import JavaScriptRuntime
from src.runtime.python_runtime import PythonRuntime
from src.runtime.registry import RuntimeRegistry, initialize_default_runtimes

__all__ = [
    "BaseRuntime",
    "PythonRuntime",
    "JavaScriptRuntime",
    "CSharpRuntime",
    "RuntimeRegistry",
    "initialize_default_runtimes",
    "DockerExecutor",  # 하위 호환성
    "SimpleExecutor",  # 하위 호환성
]

# 애플리케이션 시작 시 기본 런타임 초기화
initialize_default_runtimes()

"""
Phase 2.1 테스트: RuntimeRegistry 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry
from src.runtime.python_runtime import PythonRuntime


class TestRuntimeRegistry:
    """RuntimeRegistry 테스트"""

    def test_list_languages(self):
        """등록된 언어 목록 조회"""
        languages = RuntimeRegistry.list_languages()

        assert isinstance(languages, list)
        assert "python" in languages
        assert "py" in languages  # 별칭

    def test_is_supported(self):
        """언어 지원 여부 확인"""
        assert RuntimeRegistry.is_supported("python") is True
        assert RuntimeRegistry.is_supported("py") is True
        assert RuntimeRegistry.is_supported("PYTHON") is True  # 대소문자 무관
        assert RuntimeRegistry.is_supported("javascript") is True  # Phase 2.2
        assert RuntimeRegistry.is_supported("js") is True  # Phase 2.2
        assert RuntimeRegistry.is_supported("csharp") is True  # Phase 2.3
        assert RuntimeRegistry.is_supported("cs") is True  # Phase 2.3
        assert RuntimeRegistry.is_supported("dotnet") is True  # Phase 2.3

    def test_get_runtime(self):
        """런타임 인스턴스 조회"""
        runtime = RuntimeRegistry.get("python")

        assert runtime is not None
        assert isinstance(runtime, PythonRuntime)

    def test_get_runtime_case_insensitive(self):
        """대소문자 구분 없이 조회"""
        runtime1 = RuntimeRegistry.get("python")
        runtime2 = RuntimeRegistry.get("Python")
        runtime3 = RuntimeRegistry.get("PYTHON")

        assert runtime1 is runtime2
        assert runtime2 is runtime3

    def test_get_unsupported_language(self):
        """지원하지 않는 언어 조회 시 에러"""
        with pytest.raises(ValueError, match="Unsupported language"):
            RuntimeRegistry.get("ruby")  # 지원하지 않는 언어

    def test_python_alias(self):
        """Python 별칭 (py) 테스트"""
        python_runtime = RuntimeRegistry.get("python")
        py_runtime = RuntimeRegistry.get("py")

        assert python_runtime is py_runtime


class TestPythonRuntime:
    """PythonRuntime 테스트"""

    @pytest.fixture
    def runtime(self):
        """PythonRuntime 픽스처"""
        try:
            runtime = RuntimeRegistry.get("python")
            yield runtime
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"Python runtime not available: {e}")

    def test_language_name(self, runtime):
        """언어 이름 확인"""
        assert runtime.get_language_name() == "python"

    def test_docker_image(self, runtime):
        """Docker 이미지 확인"""
        image = runtime.get_docker_image()
        assert "python" in image.lower()

    def test_hello_world(self, runtime):
        """기본 Hello World 실행"""
        result = runtime.execute('print("Hello from PythonRuntime!")')

        assert result.exit_code == 0
        assert "Hello from PythonRuntime!" in result.stdout
        assert result.stderr == ""
        assert result.timeout is False

    def test_calculation(self, runtime):
        """계산 테스트"""
        code = """
result = 10 * 5
print(result)
"""
        result = runtime.execute(code)

        assert result.exit_code == 0
        assert "50" in result.stdout

    def test_runtime_error(self, runtime):
        """런타임 에러 처리"""
        code = "x = 1 / 0"
        result = runtime.execute(code)

        assert result.exit_code != 0
        assert "ZeroDivisionError" in result.stderr

    def test_timeout(self, runtime):
        """타임아웃 처리"""
        code = """
import time
time.sleep(10)
print("Should not reach here")
"""
        config = ExecutionConfig(timeout=2)
        result = runtime.execute(code, config)

        assert result.timeout is True
        assert result.exit_code == -1
        assert "timeout" in result.stderr.lower()

    def test_execution_duration(self, runtime):
        """실행 시간 측정"""
        code = """
import time
time.sleep(0.1)
print("Done")
"""
        result = runtime.execute(code)

        assert result.exit_code == 0
        assert result.duration_ms is not None
        assert result.duration_ms >= 100  # 최소 100ms

    def test_network_isolation(self, runtime):
        """네트워크 격리 확인"""
        code = """
import urllib.request
try:
    urllib.request.urlopen('https://google.com', timeout=2)
    print("FAIL: Network accessible")
except Exception as e:
    print(f"PASS: Network blocked - {type(e).__name__}")
"""
        result = runtime.execute(code)

        assert result.exit_code == 0
        assert "PASS" in result.stdout or "URLError" in result.stdout


class TestJavaScriptRuntime:
    """JavaScriptRuntime 테스트 (Phase 2.2)"""

    @pytest.fixture
    def runtime(self):
        """JavaScriptRuntime 픽스처"""
        try:
            runtime = RuntimeRegistry.get("javascript")
            yield runtime
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"JavaScript runtime not available: {e}")

    def test_language_name(self, runtime):
        """언어 이름 확인"""
        assert runtime.get_language_name() == "javascript"

    def test_docker_image(self, runtime):
        """Docker 이미지 확인"""
        image = runtime.get_docker_image()
        assert "nodejs" in image.lower() or "node" in image.lower()

    def test_hello_world(self, runtime):
        """기본 Hello World 실행"""
        result = runtime.execute('console.log("Hello from JavaScript!");')

        assert result.exit_code == 0
        assert "Hello from JavaScript!" in result.stdout
        assert result.timeout is False

    def test_calculation(self, runtime):
        """계산 테스트"""
        code = """
const result = 10 * 5;
console.log(result);
"""
        result = runtime.execute(code)

        assert result.exit_code == 0
        assert "50" in result.stdout

    def test_runtime_error(self, runtime):
        """런타임 에러 처리"""
        code = "throw new Error('Test error');"
        result = runtime.execute(code)

        assert result.exit_code != 0
        assert "Error" in result.stderr

    def test_timeout(self, runtime):
        """타임아웃 처리"""
        code = """
const start = Date.now();
while (Date.now() - start < 10000) {
    // 10초 대기
}
console.log("Should not reach here");
"""
        config = ExecutionConfig(timeout=2)
        result = runtime.execute(code, config)

        assert result.timeout is True
        assert result.exit_code == -1
        assert "timeout" in result.stderr.lower()

    def test_execution_duration(self, runtime):
        """실행 시간 측정"""
        code = """
setTimeout(() => {}, 100);
console.log("Done");
"""
        result = runtime.execute(code)

        assert result.exit_code == 0
        assert result.duration_ms is not None

    def test_multiple_aliases(self):
        """JavaScript 런타임 별칭 테스트"""
        try:
            js_runtime = RuntimeRegistry.get("javascript")
            js_alias = RuntimeRegistry.get("js")
            node_runtime = RuntimeRegistry.get("nodejs")
            node_alias = RuntimeRegistry.get("node")

            # 모두 같은 인스턴스
            assert js_runtime is js_alias
            assert js_runtime is node_runtime
            assert js_runtime is node_alias
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"JavaScript runtime not available: {e}")


class TestCSharpRuntime:
    """CSharpRuntime 테스트 (Phase 2.3)"""

    @pytest.fixture
    def runtime(self):
        """CSharpRuntime 픽스처"""
        try:
            runtime = RuntimeRegistry.get("csharp")
            yield runtime
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"C# runtime not available: {e}")

    def test_language_name(self, runtime):
        """언어 이름 확인"""
        assert runtime.get_language_name() == "csharp"

    def test_docker_image(self, runtime):
        """Docker 이미지 확인"""
        image = runtime.get_docker_image()
        assert "dotnet" in image.lower() or "csharp" in image.lower()

    def test_hello_world(self, runtime):
        """기본 Hello World 실행"""
        code = """
using System;
class Program
{
    static void Main()
    {
        Console.WriteLine("Hello from C#!");
    }
}
"""
        # C# 컴파일 시간 고려하여 timeout 증가
        config = ExecutionConfig(timeout=15)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Hello from C#!" in result.stdout
        assert result.timeout is False

    def test_calculation(self, runtime):
        """계산 테스트"""
        code = """
using System;
class Program
{
    static void Main()
    {
        int result = 10 * 5;
        Console.WriteLine(result);
    }
}
"""
        config = ExecutionConfig(timeout=15)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "50" in result.stdout

    def test_runtime_error(self, runtime):
        """런타임 에러 처리 (Division by zero)"""
        code = """
using System;
class Program
{
    static void Main()
    {
        int x = 1;
        int y = 0;
        int result = x / y;
        Console.WriteLine(result);
    }
}
"""
        config = ExecutionConfig(timeout=15)
        result = runtime.execute(code, config)

        assert result.exit_code != 0
        assert "DivideByZeroException" in result.stderr or "division" in result.stderr.lower()

    def test_compilation_error(self, runtime):
        """컴파일 에러 처리"""
        code = """
using System;
class Program
{
    static void Main()
    {
        Console.WriteLine("Missing semicolon")
        int x = 5;
    }
}
"""
        config = ExecutionConfig(timeout=15)
        result = runtime.execute(code, config)

        assert result.exit_code != 0
        assert "error" in result.stderr.lower()

    def test_timeout(self, runtime):
        """타임아웃 처리"""
        code = """
using System;
class Program
{
    static void Main()
    {
        while (true)
        {
            // 무한 루프
        }
        Console.WriteLine("Should not reach here");
    }
}
"""
        config = ExecutionConfig(timeout=2)
        result = runtime.execute(code, config)

        assert result.timeout is True
        assert result.exit_code == -1
        assert "timeout" in result.stderr.lower()

    def test_execution_duration(self, runtime):
        """실행 시간 측정"""
        code = """
using System;
using System.Threading;
class Program
{
    static void Main()
    {
        Thread.Sleep(100);
        Console.WriteLine("Done");
    }
}
"""
        config = ExecutionConfig(timeout=15)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Done" in result.stdout
        assert result.duration_ms is not None
        assert result.duration_ms >= 100  # 최소 100ms

    def test_multiple_aliases(self):
        """C# 런타임 별칭 테스트"""
        try:
            csharp_runtime = RuntimeRegistry.get("csharp")
            cs_runtime = RuntimeRegistry.get("cs")
            dotnet_runtime = RuntimeRegistry.get("dotnet")

            # 모두 같은 인스턴스
            assert csharp_runtime is cs_runtime
            assert csharp_runtime is dotnet_runtime
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"C# runtime not available: {e}")

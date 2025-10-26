"""
Phase 1.1 테스트: Hello World 실행기 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime.executor import SimpleExecutor, execute_python_simple


class TestSimpleExecutor:
    """SimpleExecutor 유닛 테스트"""

    def test_hello_world(self):
        """가장 기본적인 Hello World 테스트"""
        executor = SimpleExecutor()
        result = executor.execute_python('print("Hello, CodeBeaker!")')

        assert result.exit_code == 0
        assert "Hello, CodeBeaker!" in result.stdout
        assert result.stderr == ""
        assert result.timeout is False

    def test_simple_calculation(self):
        """간단한 계산 테스트"""
        code = """
result = 2 + 2
print(result)
"""
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "4" in result.stdout

    def test_multiline_output(self):
        """여러 줄 출력 테스트"""
        code = """
for i in range(3):
    print(f"Line {i}")
"""
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "Line 0" in result.stdout
        assert "Line 1" in result.stdout
        assert "Line 2" in result.stdout

    def test_stderr_output(self):
        """stderr 출력 테스트"""
        code = """
import sys
print("stdout message", file=sys.stdout)
print("stderr message", file=sys.stderr)
"""
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "stdout message" in result.stdout
        assert "stderr message" in result.stderr

    def test_syntax_error(self):
        """문법 오류 테스트"""
        code = "print('missing closing quote"
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code != 0
        assert "SyntaxError" in result.stderr or "unterminated" in result.stderr

    def test_runtime_error(self):
        """런타임 에러 테스트"""
        code = """
x = 1 / 0  # ZeroDivisionError
"""
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code != 0
        assert "ZeroDivisionError" in result.stderr

    def test_timeout(self):
        """타임아웃 테스트"""
        code = """
import time
time.sleep(10)  # 10초 대기
print("Should not reach here")
"""
        executor = SimpleExecutor()
        config = ExecutionConfig(timeout=1)  # 1초 타임아웃
        result = executor.execute_python(code, config)

        assert result.timeout is True
        assert result.exit_code == -1
        assert "timeout" in result.stderr.lower()
        assert "Should not reach here" not in result.stdout

    def test_execution_duration(self):
        """실행 시간 측정 테스트"""
        code = """
import time
time.sleep(0.1)  # 100ms 대기
print("Done")
"""
        executor = SimpleExecutor()
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert result.duration_ms is not None
        assert result.duration_ms >= 100  # 최소 100ms


class TestLegacyInterface:
    """레거시 인터페이스 테스트"""

    def test_execute_python_simple(self):
        """execute_python_simple 함수 테스트"""
        result = execute_python_simple('print("Hello")')

        assert result["exit_code"] == 0
        assert "Hello" in result["stdout"]
        assert result["stderr"] == ""

    def test_simple_timeout(self):
        """간단한 타임아웃 테스트"""
        code = """
import time
time.sleep(5)
"""
        result = execute_python_simple(code, timeout=1)

        assert result["timeout"] is True
        assert result["exit_code"] == -1

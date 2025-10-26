"""
Phase 1.4 테스트: Docker 실행기 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime.docker_executor import DockerExecutor


@pytest.fixture
def executor():
    """DockerExecutor 픽스처"""
    try:
        executor = DockerExecutor()
        yield executor
        executor.cleanup()
    except RuntimeError as e:
        pytest.skip(f"Docker not available: {e}")


class TestDockerExecutor:
    """DockerExecutor 유닛 테스트"""

    def test_hello_world(self, executor):
        """기본 Hello World 테스트"""
        result = executor.execute_python('print("Hello from Docker!")')

        assert result.exit_code == 0
        assert "Hello from Docker!" in result.stdout
        assert result.stderr == ""
        assert result.timeout is False

    def test_calculation(self, executor):
        """계산 테스트"""
        code = """
result = 2 + 2
print(result)
"""
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "4" in result.stdout

    def test_multiline_output(self, executor):
        """여러 줄 출력 테스트"""
        code = """
for i in range(3):
    print(f"Line {i}")
"""
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "Line 0" in result.stdout
        assert "Line 1" in result.stdout
        assert "Line 2" in result.stdout

    def test_runtime_error(self, executor):
        """런타임 에러 테스트"""
        code = "x = 1 / 0"
        result = executor.execute_python(code)

        assert result.exit_code != 0
        assert "ZeroDivisionError" in result.stderr

    def test_syntax_error(self, executor):
        """문법 에러 테스트"""
        code = "print('missing closing quote"
        result = executor.execute_python(code)

        assert result.exit_code != 0
        assert "SyntaxError" in result.stderr or "unterminated" in result.stderr

    def test_timeout(self, executor):
        """타임아웃 테스트"""
        code = """
import time
time.sleep(10)
print("Should not reach here")
"""
        config = ExecutionConfig(timeout=2)
        result = executor.execute_python(code, config)

        assert result.timeout is True
        assert result.exit_code == -1
        assert "timeout" in result.stderr.lower()

    def test_network_isolation(self, executor):
        """네트워크 격리 테스트"""
        code = """
import urllib.request
try:
    urllib.request.urlopen('https://google.com', timeout=2)
    print("FAIL: Network accessible")
except Exception as e:
    print(f"PASS: Network blocked - {type(e).__name__}")
"""
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert "PASS" in result.stdout or "URLError" in result.stdout

    def test_execution_duration(self, executor):
        """실행 시간 측정 테스트"""
        code = """
import time
time.sleep(0.1)
print("Done")
"""
        result = executor.execute_python(code)

        assert result.exit_code == 0
        assert result.duration_ms is not None
        assert result.duration_ms >= 100  # 최소 100ms

    def test_file_system_read_only(self, executor):
        """파일시스템 읽기 전용 테스트"""
        code = """
try:
    with open('/workspace/test.txt', 'w') as f:
        f.write('test')
    print("FAIL: Filesystem writable")
except Exception as e:
    print(f"PASS: Filesystem read-only - {type(e).__name__}")
"""
        result = executor.execute_python(code)

        assert result.exit_code == 0
        # 읽기 전용이므로 쓰기 실패해야 함
        assert "PASS" in result.stdout or "PermissionError" in result.stdout

    def test_context_manager(self):
        """컨텍스트 매니저 테스트"""
        try:
            with DockerExecutor() as executor:
                result = executor.execute_python('print("Test")')
                assert result.exit_code == 0
        except RuntimeError as e:
            pytest.skip(f"Docker not available: {e}")

    def test_memory_limit(self, executor):
        """메모리 제한 테스트 (간단한 확인)"""
        code = """
# 작은 메모리 사용
x = [0] * 1000
print("Memory test passed")
"""
        config = ExecutionConfig(memory_limit="256m")
        result = executor.execute_python(code, config)

        assert result.exit_code == 0
        assert "Memory test passed" in result.stdout

    def test_cpu_limit(self, executor):
        """CPU 제한 테스트 (간단한 확인)"""
        code = """
# CPU 사용
sum(range(10000))
print("CPU test passed")
"""
        config = ExecutionConfig(cpu_limit=0.5)
        result = executor.execute_python(code, config)

        assert result.exit_code == 0
        assert "CPU test passed" in result.stdout

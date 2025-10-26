"""
Go 런타임 테스트

GoRuntime 유닛 테스트
"""

import pytest

from src.common.models import ExecutionConfig
from src.runtime import RuntimeRegistry


class TestGoRuntime:
    """GoRuntime 테스트"""

    @pytest.fixture
    def runtime(self):
        """Go 런타임 fixture"""
        try:
            runtime = RuntimeRegistry.get("go")
            yield runtime
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"Go runtime not available: {e}")

    def test_hello_world(self, runtime):
        """Hello World 실행"""
        code = """
package main

import "fmt"

func main() {
    fmt.Println("Hello from Go!")
}
"""
        config = ExecutionConfig(timeout=25)  # Go 컴파일 시간 고려 (17초 정도 필요)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Hello from Go!" in result.stdout
        assert result.timeout is False

    def test_calculation(self, runtime):
        """계산 실행"""
        code = """
package main

import "fmt"

func main() {
    sum := 10 + 20
    product := 5 * 6
    fmt.Printf("Sum: %d\\n", sum)
    fmt.Printf("Product: %d\\n", product)
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Sum: 30" in result.stdout
        assert "Product: 30" in result.stdout

    def test_multiple_lines(self, runtime):
        """여러 줄 출력"""
        code = """
package main

import "fmt"

func main() {
    for i := 1; i <= 5; i++ {
        fmt.Printf("Line %d\\n", i)
    }
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        for i in range(1, 6):
            assert f"Line {i}" in result.stdout

    def test_syntax_error(self, runtime):
        """구문 오류 처리"""
        code = """
package main

import "fmt"

func main() {
    fmt.Println("Missing closing quote)
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code != 0
        assert len(result.stderr) > 0

    def test_runtime_error(self, runtime):
        """런타임 에러 처리"""
        code = """
package main

import "fmt"

func main() {
    var arr []int
    fmt.Println(arr[10])  // Index out of range
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code != 0
        assert "panic" in result.stderr or "runtime error" in result.stderr

    def test_timeout(self, runtime):
        """타임아웃 처리"""
        code = """
package main

import "time"

func main() {
    time.Sleep(10 * time.Second)
}
"""
        config = ExecutionConfig(timeout=2)
        result = runtime.execute(code, config)

        assert result.timeout is True

    def test_stderr_output(self, runtime):
        """표준 에러 출력"""
        code = """
package main

import (
    "fmt"
    "os"
)

func main() {
    fmt.Fprintln(os.Stderr, "Error message")
    fmt.Println("Normal output")
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "Normal output" in result.stdout
        assert "Error message" in result.stderr

    def test_string_manipulation(self, runtime):
        """문자열 처리"""
        code = """
package main

import (
    "fmt"
    "strings"
)

func main() {
    text := "hello world"
    upper := strings.ToUpper(text)
    fmt.Println(upper)
}
"""
        config = ExecutionConfig(timeout=25)
        result = runtime.execute(code, config)

        assert result.exit_code == 0
        assert "HELLO WORLD" in result.stdout

    def test_golang_alias(self):
        """golang 별칭 테스트"""
        try:
            runtime = RuntimeRegistry.get("golang")
            assert runtime is not None
            assert runtime.get_language_name() == "go"
        except (RuntimeError, ValueError) as e:
            pytest.skip(f"Go runtime not available: {e}")


class TestGoRuntimeRegistry:
    """GoRuntime 레지스트리 테스트"""

    def test_go_registered(self):
        """go 언어 등록 확인"""
        languages = RuntimeRegistry.list_languages()
        # Go 런타임이 있으면 확인
        if "go" in languages:
            assert RuntimeRegistry.is_supported("go")
            assert RuntimeRegistry.is_supported("golang")

    def test_get_go_runtime(self):
        """Go 런타임 가져오기"""
        if RuntimeRegistry.is_supported("go"):
            runtime = RuntimeRegistry.get("go")
            assert runtime.get_language_name() == "go"
            assert runtime.get_docker_image() == "codebeaker-golang:latest"

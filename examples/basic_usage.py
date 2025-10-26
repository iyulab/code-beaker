"""
CodeBeaker 기본 사용 예제
"""

import sys
from pathlib import Path

# 프로젝트 루트를 sys.path에 추가
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from src.runtime.executor import SimpleExecutor
from src.common.models import ExecutionConfig


def example_1_hello_world():
    """예제 1: Hello World"""
    print("=" * 60)
    print("예제 1: Hello World")
    print("=" * 60)

    executor = SimpleExecutor()
    result = executor.execute_python('print("Hello, CodeBeaker!")')

    print(f"출력: {result.stdout}")
    print(f"실행 시간: {result.duration_ms}ms")
    print()


def example_2_calculation():
    """예제 2: 계산"""
    print("=" * 60)
    print("예제 2: 피보나치 수열")
    print("=" * 60)

    code = """
def fibonacci(n):
    if n <= 1:
        return n
    return fibonacci(n-1) + fibonacci(n-2)

for i in range(10):
    print(f"fibonacci({i}) = {fibonacci(i)}")
"""

    executor = SimpleExecutor()
    result = executor.execute_python(code)

    print(result.stdout)
    print(f"실행 시간: {result.duration_ms}ms")
    print()


def example_3_timeout():
    """예제 3: 타임아웃 처리"""
    print("=" * 60)
    print("예제 3: 타임아웃 처리")
    print("=" * 60)

    code = """
import time
print("Starting...")
time.sleep(10)
print("This should not appear")
"""

    executor = SimpleExecutor()
    config = ExecutionConfig(timeout=2)  # 2초 타임아웃
    result = executor.execute_python(code, config)

    print(f"타임아웃 발생: {result.timeout}")
    print(f"에러 메시지: {result.stderr}")
    print(f"실행 시간: {result.duration_ms}ms")
    print()


def example_4_error_handling():
    """예제 4: 에러 처리"""
    print("=" * 60)
    print("예제 4: 에러 처리")
    print("=" * 60)

    # 런타임 에러
    code = "x = 1 / 0"

    executor = SimpleExecutor()
    result = executor.execute_python(code)

    print(f"성공: {result.exit_code == 0}")
    print(f"에러 타입: {result.error_type}")
    print(f"에러 메시지:\n{result.stderr}")
    print()


def example_5_api_client():
    """예제 5: API 클라이언트"""
    print("=" * 60)
    print("예제 5: HTTP API 사용")
    print("=" * 60)

    try:
        import requests

        response = requests.post(
            "http://localhost:8000/execute",
            json={"code": 'print("Hello from API!")', "language": "python"},
        )

        if response.status_code == 200:
            result = response.json()
            print(f"성공: {result['success']}")
            print(f"출력: {result['stdout']}")
            print(f"실행 시간: {result['duration_ms']}ms")
        else:
            print(f"에러: HTTP {response.status_code}")

    except Exception as e:
        print(f"API 서버가 실행 중이지 않습니다: {e}")
        print("먼저 'python scripts/run_api.py'로 서버를 실행하세요.")

    print()


if __name__ == "__main__":
    example_1_hello_world()
    example_2_calculation()
    example_3_timeout()
    example_4_error_handling()
    example_5_api_client()

    print("=" * 60)
    print("모든 예제 완료!")
    print("=" * 60)

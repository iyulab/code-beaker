"""API 클라이언트 테스트 스크립트"""

import requests
import json


def test_api():
    """API 서버에 실제 HTTP 요청 테스트"""
    base_url = "http://localhost:8000"

    print("=" * 60)
    print("CodeBeaker API 테스트 클라이언트")
    print("=" * 60)

    # 1. 헬스 체크
    print("\n1. 헬스 체크...")
    response = requests.get(f"{base_url}/health")
    print(f"   상태 코드: {response.status_code}")
    print(f"   응답: {response.json()}")

    # 2. Hello World 실행
    print("\n2. Hello World 실행...")
    response = requests.post(
        f"{base_url}/execute",
        json={"code": 'print("Hello, CodeBeaker!")', "language": "python"},
    )
    print(f"   상태 코드: {response.status_code}")
    result = response.json()
    print(f"   성공: {result['success']}")
    print(f"   출력: {result['stdout']}")
    print(f"   실행 시간: {result['duration_ms']}ms")

    # 3. 계산 실행
    print("\n3. 계산 실행...")
    code = """
def fibonacci(n):
    if n <= 1:
        return n
    return fibonacci(n-1) + fibonacci(n-2)

for i in range(10):
    print(f"fibonacci({i}) = {fibonacci(i)}")
"""
    response = requests.post(f"{base_url}/execute", json={"code": code})
    result = response.json()
    print(f"   성공: {result['success']}")
    print(f"   출력:\n{result['stdout']}")
    print(f"   실행 시간: {result['duration_ms']}ms")

    # 4. 에러 테스트
    print("\n4. 에러 처리 테스트...")
    response = requests.post(
        f"{base_url}/execute", json={"code": "x = 1 / 0", "language": "python"}
    )
    result = response.json()
    print(f"   성공: {result['success']}")
    print(f"   에러 타입: {result['error_type']}")
    print(f"   에러 메시지: {result['stderr'][:100]}...")

    # 5. 타임아웃 테스트
    print("\n5. 타임아웃 테스트...")
    response = requests.post(
        f"{base_url}/execute",
        json={"code": "import time; time.sleep(10)", "timeout": 1},
    )
    result = response.json()
    print(f"   타임아웃 발생: {result['timeout']}")
    print(f"   메시지: {result['stderr']}")

    # 6. API 문서 확인
    print("\n6. API 문서 확인...")
    print(f"   Swagger UI: {base_url}/docs")
    print(f"   ReDoc: {base_url}/redoc")

    print("\n" + "=" * 60)
    print("테스트 완료!")
    print("=" * 60)


if __name__ == "__main__":
    try:
        test_api()
    except requests.exceptions.ConnectionError:
        print("\n❌ API 서버에 연결할 수 없습니다!")
        print("   다음 명령으로 서버를 먼저 실행하세요:")
        print("   python scripts/run_api.py")

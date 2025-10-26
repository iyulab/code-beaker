# 빠른 시작 가이드

## CodeBeaker API 사용하기

### 1. API 서버 실행

```bash
# 방법 1: Python으로 직접 실행
python scripts/run_api.py

# 방법 2: uvicorn으로 실행
uvicorn src.api.main:app --reload --host 0.0.0.0 --port 8000
```

서버가 실행되면 다음 주소에서 접근할 수 있습니다:
- API: http://localhost:8000
- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

### 2. HTTP로 코드 실행하기

#### curl 예시

```bash
# Hello World
curl -X POST http://localhost:8000/execute \
  -H "Content-Type: application/json" \
  -d '{
    "code": "print(\"Hello, CodeBeaker!\")",
    "language": "python"
  }'

# 응답:
# {
#   "success": true,
#   "stdout": "Hello, CodeBeaker!\n",
#   "stderr": "",
#   "exit_code": 0,
#   "duration_ms": 45,
#   "timeout": false,
#   "error_type": null
# }
```

#### Python requests 예시

```python
import requests

response = requests.post(
    'http://localhost:8000/execute',
    json={
        'code': '''
def fibonacci(n):
    if n <= 1:
        return n
    return fibonacci(n-1) + fibonacci(n-2)

for i in range(10):
    print(f"fib({i}) = {fibonacci(i)}")
''',
        'language': 'python',
        'timeout': 5
    }
)

result = response.json()
print(f"성공: {result['success']}")
print(f"출력:\n{result['stdout']}")
print(f"실행 시간: {result['duration_ms']}ms")
```

#### JavaScript fetch 예시

```javascript
fetch('http://localhost:8000/execute', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    code: 'for i in range(5):\n    print(f"Count: {i}")',
    language: 'python',
    timeout: 5
  })
})
.then(response => response.json())
.then(data => {
  console.log('성공:', data.success);
  console.log('출력:', data.stdout);
  console.log('실행 시간:', data.duration_ms, 'ms');
});
```

### 3. API 엔드포인트

#### GET /health
헬스 체크

```bash
curl http://localhost:8000/health
```

응답:
```json
{
  "status": "healthy",
  "version": "0.1.0"
}
```

#### POST /execute
코드 실행

**요청 본문**:
```json
{
  "code": "print('Hello')",      // 필수: 실행할 코드
  "language": "python",           // 선택: 언어 (기본값: python)
  "timeout": 5                    // 선택: 타임아웃 초 (기본값: 5, 범위: 1-30)
}
```

**응답**:
```json
{
  "success": true,                // 실행 성공 여부
  "stdout": "Hello\n",            // 표준 출력
  "stderr": "",                   // 표준 에러
  "exit_code": 0,                 // 종료 코드
  "duration_ms": 45,              // 실행 시간 (밀리초)
  "timeout": false,               // 타임아웃 발생 여부
  "error_type": null              // 에러 타입 (있는 경우)
}
```

### 4. 테스트 클라이언트 실행

자동화된 테스트 클라이언트를 실행하여 API를 테스트할 수 있습니다:

```bash
# API 서버가 실행 중이어야 합니다
python scripts/test_api_client.py
```

출력 예시:
```
============================================================
CodeBeaker API 테스트 클라이언트
============================================================

1. 헬스 체크...
   상태 코드: 200
   응답: {'status': 'healthy', 'version': '0.1.0'}

2. Hello World 실행...
   상태 코드: 200
   성공: True
   출력: Hello, CodeBeaker!
   실행 시간: 45ms

3. 계산 실행...
   성공: True
   출력:
   fibonacci(0) = 0
   fibonacci(1) = 1
   ...
   실행 시간: 123ms

...
```

### 5. Python에서 직접 사용하기

API 서버 없이 직접 실행기를 사용할 수도 있습니다:

```python
from src.runtime.executor import SimpleExecutor
from src.common.models import ExecutionConfig

# 실행기 생성
executor = SimpleExecutor()

# 코드 실행
result = executor.execute_python('print("Hello, CodeBeaker!")')

print(f"출력: {result.stdout}")
print(f"실행 시간: {result.duration_ms}ms")
print(f"성공: {result.exit_code == 0}")

# 타임아웃 설정
config = ExecutionConfig(timeout=1)
result = executor.execute_python('import time; time.sleep(10)', config)
print(f"타임아웃: {result.timeout}")
```

### 6. 에러 처리

#### 타임아웃

```bash
curl -X POST http://localhost:8000/execute \
  -H "Content-Type: application/json" \
  -d '{
    "code": "import time; time.sleep(10)",
    "timeout": 1
  }'
```

응답:
```json
{
  "success": false,
  "timeout": true,
  "stderr": "Execution timeout after 1 seconds",
  "exit_code": -1,
  "error_type": "TimeoutError"
}
```

#### 런타임 에러

```bash
curl -X POST http://localhost:8000/execute \
  -H "Content-Type: application/json" \
  -d '{
    "code": "x = 1 / 0"
  }'
```

응답:
```json
{
  "success": false,
  "exit_code": 1,
  "stderr": "...ZeroDivisionError: division by zero...",
  "error_type": null
}
```

#### 지원하지 않는 언어

```bash
curl -X POST http://localhost:8000/execute \
  -H "Content-Type: application/json" \
  -d '{
    "code": "console.log(\"test\")",
    "language": "javascript"
  }'
```

응답 (400 Bad Request):
```json
{
  "detail": "Unsupported language: javascript. Currently only Python is supported."
}
```

### 7. 다음 단계

- **Phase 1.4**: Docker 컨테이너 격리로 진정한 샌드박싱 구현
- **Phase 2**: JavaScript, C# 등 다중 언어 지원 추가
- **Phase 3**: 비동기 실행 및 큐 시스템 구현

자세한 로드맵은 [docs/TASKS.md](docs/TASKS.md)를 참조하세요.

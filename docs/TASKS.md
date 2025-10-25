# CodeBeaker 구현 로드맵

## 개요

이 문서는 CodeBeaker의 단계별 구현 계획을 정의합니다. 각 페이즈는 **단순한 문제에서 시작하여 점진적으로 복잡도를 증가**시키는 방식으로 구성되어 있습니다.

### 개발 원칙

1. **점진적 복잡도**: 각 페이즈는 이전 페이즈의 기능을 기반으로 구축
2. **검증된 마일스톤**: 각 페이즈는 실제 동작하는 시스템을 산출
3. **실용주의**: 프로토타이핑 → 기본 구현 → 최적화 → 프로덕션 강화 순서
4. **조기 피드백**: 각 페이즈 완료 후 실제 사용 사례로 검증

---

## Phase 0: 프로젝트 설정 (1주)

**목표**: 개발 환경 구성 및 기본 인프라 설정

### 0.1 프로젝트 구조 생성

**난이도**: ⭐ (매우 단순)

**작업**:
```
codebeaker/
├── src/
│   ├── api/          # API 계층
│   ├── worker/       # 워커 계층
│   ├── runtime/      # 런타임 어댑터
│   └── common/       # 공통 유틸리티
├── tests/
│   ├── unit/
│   └── integration/
├── docker/
│   └── runtimes/     # 언어별 Docker 이미지
├── docs/
└── scripts/
```

**산출물**:
- 기본 디렉토리 구조
- README.md, LICENSE
- .gitignore, .editorconfig

### 0.2 개발 도구 설정

**난이도**: ⭐ (매우 단순)

**작업**:
- Git 저장소 초기화
- 언어 선택 (추천: Python 또는 Go)
- 린팅 및 포맷팅 도구 (black, pylint, mypy 또는 golangci-lint)
- 테스트 프레임워크 (pytest 또는 go test)

**검증**:
```bash
# Python 예시
pytest tests/
black --check src/
mypy src/

# Go 예시
go test ./...
golangci-lint run
```

### 0.3 Docker 환경 설정

**난이도**: ⭐⭐ (단순)

**작업**:
- Docker Desktop 설치 및 설정
- docker-compose.yml 작성 (PostgreSQL, Redis)
- 기본 네트워크 구성

**검증**:
```bash
docker-compose up -d
docker ps  # PostgreSQL, Redis 실행 확인
```

---

## Phase 1: 최소 실행 가능 제품 (MVP) (2-4주)

**목표**: 단일 언어(Python)로 코드를 실행하고 결과를 반환하는 기본 시스템

### 1.1 "Hello World" 실행기

**난이도**: ⭐⭐ (단순)

**문제**: Python 코드를 받아서 실행하고 stdout을 반환

**작업**:
1. Python 코드를 임시 파일로 저장
2. `subprocess.run()`으로 Python 실행
3. stdout, stderr, exit_code 수집
4. 결과 반환

**코드 예시**:
```python
def execute_python(code: str) -> dict:
    """가장 단순한 Python 실행기"""
    with tempfile.NamedTemporaryFile(mode='w', suffix='.py', delete=False) as f:
        f.write(code)
        temp_path = f.name

    try:
        result = subprocess.run(
            ['python', temp_path],
            capture_output=True,
            text=True,
            timeout=5
        )
        return {
            'stdout': result.stdout,
            'stderr': result.stderr,
            'exit_code': result.returncode
        }
    finally:
        os.unlink(temp_path)
```

**검증**:
```python
result = execute_python('print("Hello, CodeBeaker!")')
assert result['stdout'] == 'Hello, CodeBeaker!\n'
assert result['exit_code'] == 0
```

### 1.2 기본 리소스 제한

**난이도**: ⭐⭐⭐ (중간)

**문제**: 무한 루프나 메모리 폭탄으로부터 호스트 보호

**작업**:
1. 타임아웃 구현 (`timeout` 파라미터)
2. 메모리 제한 (리눅스: `resource.setrlimit`)
3. 제한 초과 시 적절한 에러 반환

**코드 예시**:
```python
import resource

def set_resource_limits():
    """자식 프로세스의 리소스 제한 설정"""
    # 메모리 제한: 256MB
    resource.setrlimit(resource.RLIMIT_AS, (256 * 1024 * 1024, 256 * 1024 * 1024))
    # CPU 시간 제한: 5초
    resource.setrlimit(resource.RLIMIT_CPU, (5, 5))

result = subprocess.run(
    ['python', temp_path],
    capture_output=True,
    text=True,
    timeout=10,  # wall-time 제한
    preexec_fn=set_resource_limits  # 리눅스 전용
)
```

**검증**:
```python
# 무한 루프 테스트
code = "while True: pass"
result = execute_python(code)
assert result['timeout'] == True

# 메모리 폭탄 테스트
code = "x = [0] * (10**9)"  # 거대한 리스트
result = execute_python(code)
assert 'MemoryError' in result['stderr']
```

### 1.3 간단한 REST API

**난이도**: ⭐⭐ (단순)

**문제**: HTTP API를 통해 코드 실행 요청 받기

**작업**:
1. Flask 또는 FastAPI로 API 서버 구현
2. POST /execute 엔드포인트
3. 요청 검증 (코드 크기 제한, 필수 필드)

**코드 예시**:
```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI()

class ExecuteRequest(BaseModel):
    code: str = Field(..., max_length=10000)  # 10KB 제한
    timeout: int = Field(default=5, ge=1, le=30)

@app.post("/execute")
def execute_code(request: ExecuteRequest):
    try:
        result = execute_python(request.code, timeout=request.timeout)
        return result
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
```

**검증**:
```bash
curl -X POST http://localhost:8000/execute \
  -H "Content-Type: application/json" \
  -d '{"code": "print(2 + 2)"}'

# 응답:
# {"stdout": "4\n", "stderr": "", "exit_code": 0}
```

### 1.4 Docker 컨테이너 격리

**난이도**: ⭐⭐⭐⭐ (복잡)

**문제**: 프로세스 격리에서 컨테이너 격리로 전환

**작업**:
1. Python 실행 전용 Dockerfile 작성
2. Docker Python SDK로 컨테이너 생성/실행
3. 코드를 볼륨 마운트로 전달
4. stdout/stderr 수집 및 컨테이너 정리

**Dockerfile**:
```dockerfile
FROM python:3.11-slim

# 보안: 비root 사용자
RUN useradd -m -u 1000 coderunner
USER coderunner

WORKDIR /workspace
CMD ["python", "/workspace/code.py"]
```

**코드 예시**:
```python
import docker

def execute_in_container(code: str, timeout: int = 5) -> dict:
    client = docker.from_env()

    # 임시 디렉토리에 코드 작성
    with tempfile.TemporaryDirectory() as tmpdir:
        code_path = os.path.join(tmpdir, 'code.py')
        with open(code_path, 'w') as f:
            f.write(code)

        try:
            container = client.containers.run(
                'python:3.11-slim',
                command=['python', '/code/code.py'],
                volumes={tmpdir: {'bind': '/code', 'mode': 'ro'}},
                mem_limit='256m',
                nano_cpus=int(0.5 * 1e9),  # 0.5 CPU
                network_disabled=True,
                remove=True,
                detach=False,
                stdout=True,
                stderr=True,
                timeout=timeout
            )
            return {
                'stdout': container.decode('utf-8'),
                'exit_code': 0
            }
        except docker.errors.ContainerError as e:
            return {
                'stderr': e.stderr.decode('utf-8'),
                'exit_code': e.exit_status
            }
```

**검증**:
```python
# 네트워크 차단 확인
code = """
import urllib.request
urllib.request.urlopen('https://google.com')
"""
result = execute_in_container(code)
assert 'URLError' in result['stderr']  # 네트워크 접근 차단됨
```

**Phase 1 마일스톤**:
- ✅ Python 코드를 Docker 컨테이너에서 실행
- ✅ 기본 리소스 제한 (CPU, 메모리, 타임아웃)
- ✅ REST API를 통한 실행 요청
- ✅ 네트워크 격리

---

## Phase 2: 다중 언어 지원 (4-6주)

**목표**: Python, C#, JavaScript 런타임 추가 및 공통 인터페이스 설계

### 2.1 런타임 추상화 설계

**난이도**: ⭐⭐⭐ (중간)

**문제**: 언어별로 다른 실행 방식을 통일된 인터페이스로 추상화

**작업**:
1. `BaseRuntime` 추상 클래스 정의
2. `PythonRuntime` 구현 (Phase 1 코드 리팩터링)
3. 런타임 레지스트리 패턴

**코드 예시**:
```python
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional

@dataclass
class ExecutionResult:
    stdout: str
    stderr: str
    exit_code: int
    duration_ms: int
    memory_mb: Optional[int] = None

class BaseRuntime(ABC):
    """모든 언어 런타임의 기본 인터페이스"""

    @abstractmethod
    def get_docker_image(self) -> str:
        """사용할 Docker 이미지 이름"""
        pass

    @abstractmethod
    def prepare_code(self, code: str, tmpdir: str) -> str:
        """코드를 파일로 저장하고 엔트리 포인트 반환"""
        pass

    @abstractmethod
    def get_run_command(self, entry_point: str) -> list[str]:
        """실행 명령어 생성"""
        pass

    def execute(self, code: str, config: dict) -> ExecutionResult:
        """공통 실행 로직 (템플릿 메서드 패턴)"""
        # 모든 런타임이 공유하는 실행 로직
        pass

class PythonRuntime(BaseRuntime):
    def get_docker_image(self) -> str:
        return 'python:3.11-slim'

    def prepare_code(self, code: str, tmpdir: str) -> str:
        code_path = os.path.join(tmpdir, 'main.py')
        with open(code_path, 'w') as f:
            f.write(code)
        return '/code/main.py'

    def get_run_command(self, entry_point: str) -> list[str]:
        return ['python', entry_point]

# 런타임 레지스트리
class RuntimeRegistry:
    _runtimes = {}

    @classmethod
    def register(cls, language: str, runtime: BaseRuntime):
        cls._runtimes[language] = runtime

    @classmethod
    def get(cls, language: str) -> BaseRuntime:
        if language not in cls._runtimes:
            raise ValueError(f"Unknown language: {language}")
        return cls._runtimes[language]

# 등록
RuntimeRegistry.register('python', PythonRuntime())
```

**검증**:
```python
runtime = RuntimeRegistry.get('python')
result = runtime.execute('print("Hello")', {})
assert result.exit_code == 0
```

### 2.2 JavaScript/Node.js 런타임

**난이도**: ⭐⭐ (단순)

**문제**: Node.js 코드 실행 지원

**Dockerfile**:
```dockerfile
FROM node:20-slim

RUN useradd -m -u 1000 coderunner
USER coderunner

WORKDIR /workspace
CMD ["node", "/workspace/main.js"]
```

**런타임 구현**:
```python
class JavaScriptRuntime(BaseRuntime):
    def get_docker_image(self) -> str:
        return 'node:20-slim'

    def prepare_code(self, code: str, tmpdir: str) -> str:
        code_path = os.path.join(tmpdir, 'main.js')
        with open(code_path, 'w') as f:
            f.write(code)
        return '/code/main.js'

    def get_run_command(self, entry_point: str) -> list[str]:
        return ['node', entry_point]

RuntimeRegistry.register('javascript', JavaScriptRuntime())
RuntimeRegistry.register('nodejs', JavaScriptRuntime())  # 별칭
```

**검증**:
```python
result = RuntimeRegistry.get('javascript').execute(
    'console.log("Hello from Node.js")',
    {}
)
assert 'Hello from Node.js' in result.stdout
```

### 2.3 C# 런타임

**난이도**: ⭐⭐⭐ (중간)

**문제**: .NET 컴파일 및 실행

**Dockerfile**:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
FROM mcr.microsoft.com/dotnet/runtime:8.0

RUN useradd -m -u 1000 coderunner
USER coderunner

WORKDIR /workspace
```

**런타임 구현**:
```python
class CSharpRuntime(BaseRuntime):
    def get_docker_image(self) -> str:
        return 'mcr.microsoft.com/dotnet/sdk:8.0'

    def prepare_code(self, code: str, tmpdir: str) -> str:
        # .csproj 파일 생성
        csproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """
        with open(os.path.join(tmpdir, 'Program.csproj'), 'w') as f:
            f.write(csproj)

        # Program.cs 파일 생성
        code_path = os.path.join(tmpdir, 'Program.cs')
        with open(code_path, 'w') as f:
            f.write(code)

        return '/code'

    def get_run_command(self, entry_point: str) -> list[str]:
        return [
            'sh', '-c',
            'cd /code && dotnet build -c Release && dotnet run -c Release --no-build'
        ]

RuntimeRegistry.register('csharp', CSharpRuntime())
RuntimeRegistry.register('cs', CSharpRuntime())
```

**검증**:
```python
code = """
using System;
class Program {
    static void Main() {
        Console.WriteLine("Hello from C#");
    }
}
"""
result = RuntimeRegistry.get('csharp').execute(code, {})
assert 'Hello from C#' in result.stdout
```

### 2.4 의존성 설치 지원

**난이도**: ⭐⭐⭐⭐ (복잡)

**문제**: 사용자가 외부 패키지를 설치할 수 있도록 지원

**Python 예시**:
```python
class ExecuteRequest(BaseModel):
    code: str
    language: str
    packages: Optional[List[str]] = None  # ["numpy", "pandas"]

class PythonRuntime(BaseRuntime):
    ALLOWED_PACKAGES = {
        'numpy', 'pandas', 'requests', 'scipy', 'matplotlib'
    }

    def prepare_code(self, code: str, tmpdir: str, packages: List[str] = None) -> str:
        # requirements.txt 생성
        if packages:
            # 화이트리스트 검증
            invalid = set(packages) - self.ALLOWED_PACKAGES
            if invalid:
                raise ValueError(f"Packages not allowed: {invalid}")

            req_path = os.path.join(tmpdir, 'requirements.txt')
            with open(req_path, 'w') as f:
                f.write('\n'.join(packages))

        # 코드 작성
        code_path = os.path.join(tmpdir, 'main.py')
        with open(code_path, 'w') as f:
            f.write(code)

        return '/code/main.py'

    def get_run_command(self, entry_point: str, packages: List[str] = None) -> list[str]:
        if packages:
            return [
                'sh', '-c',
                'pip install --no-deps -r /code/requirements.txt && python /code/main.py'
            ]
        return ['python', entry_point]
```

**검증**:
```python
code = """
import numpy as np
print(np.array([1, 2, 3]).sum())
"""
result = RuntimeRegistry.get('python').execute(
    code,
    {'packages': ['numpy']}
)
assert '6' in result.stdout
```

**Phase 2 마일스톤**:
- ✅ 3개 언어 지원 (Python, JavaScript, C#)
- ✅ 통일된 런타임 인터페이스
- ✅ 패키지 설치 지원 (화이트리스트)
- ✅ 언어별 Docker 이미지 최적화

---

## Phase 3: 비동기 실행 및 큐 시스템 (3-4주)

**목표**: 장기 실행 작업을 위한 메시지 큐 및 워커 풀 구현

### 3.1 간단한 작업 큐 (Redis)

**난이도**: ⭐⭐ (단순)

**문제**: 실행 요청을 큐에 넣고 백그라운드에서 처리

**작업**:
1. Redis를 작업 큐로 사용
2. 실행 요청을 직렬화하여 큐에 삽입
3. 작업 ID를 즉시 반환

**코드 예시**:
```python
import redis
import uuid
import json

redis_client = redis.Redis(host='localhost', port=6379, decode_responses=True)

@app.post("/execute/async")
def execute_async(request: ExecuteRequest):
    # 고유 실행 ID 생성
    execution_id = str(uuid.uuid4())

    # 작업 데이터 직렬화
    task = {
        'execution_id': execution_id,
        'code': request.code,
        'language': request.language,
        'packages': request.packages or [],
        'timeout': request.timeout,
        'created_at': datetime.utcnow().isoformat()
    }

    # Redis 큐에 삽입
    redis_client.rpush('codebeaker:queue', json.dumps(task))

    return {
        'execution_id': execution_id,
        'status': 'queued'
    }
```

**검증**:
```bash
curl -X POST http://localhost:8000/execute/async \
  -H "Content-Type: application/json" \
  -d '{"code": "import time; time.sleep(10); print(\"Done\")", "language": "python"}'

# 응답:
# {"execution_id": "a1b2c3d4-...", "status": "queued"}
```

### 3.2 기본 워커 구현

**난이도**: ⭐⭐⭐ (중간)

**문제**: 큐에서 작업을 가져와 실행하는 워커 프로세스

**코드 예시**:
```python
# worker.py
import redis
import json
import time

redis_client = redis.Redis(host='localhost', port=6379, decode_responses=True)

def worker_loop():
    print("Worker started, waiting for tasks...")

    while True:
        # 블로킹 방식으로 큐에서 작업 가져오기 (타임아웃 1초)
        task_data = redis_client.blpop('codebeaker:queue', timeout=1)

        if not task_data:
            continue  # 큐가 비어있음

        queue_name, task_json = task_data
        task = json.loads(task_json)

        execution_id = task['execution_id']
        print(f"Processing execution: {execution_id}")

        try:
            # 상태 업데이트: queued -> running
            redis_client.hset(
                f'execution:{execution_id}',
                mapping={'status': 'running', 'started_at': datetime.utcnow().isoformat()}
            )

            # 코드 실행
            runtime = RuntimeRegistry.get(task['language'])
            result = runtime.execute(task['code'], task)

            # 결과 저장
            redis_client.hset(
                f'execution:{execution_id}',
                mapping={
                    'status': 'completed',
                    'stdout': result.stdout,
                    'stderr': result.stderr,
                    'exit_code': result.exit_code,
                    'duration_ms': result.duration_ms,
                    'completed_at': datetime.utcnow().isoformat()
                }
            )

            print(f"Completed execution: {execution_id}")

        except Exception as e:
            # 실행 실패
            redis_client.hset(
                f'execution:{execution_id}',
                mapping={
                    'status': 'failed',
                    'error': str(e),
                    'failed_at': datetime.utcnow().isoformat()
                }
            )
            print(f"Failed execution: {execution_id}, error: {e}")

if __name__ == '__main__':
    worker_loop()
```

**실행**:
```bash
# 터미널 1: API 서버
python api.py

# 터미널 2: 워커
python worker.py

# 터미널 3: 요청 전송
curl -X POST http://localhost:8000/execute/async \
  -d '{"code": "print(\"Hello\")", "language": "python"}'
```

### 3.3 결과 조회 API

**난이도**: ⭐⭐ (단순)

**문제**: 실행 ID로 결과 조회

**코드 예시**:
```python
@app.get("/execution/{execution_id}")
def get_execution(execution_id: str):
    result = redis_client.hgetall(f'execution:{execution_id}')

    if not result:
        raise HTTPException(status_code=404, detail="Execution not found")

    return result
```

**검증**:
```bash
# 실행 요청
EXEC_ID=$(curl -X POST http://localhost:8000/execute/async \
  -d '{"code": "print(2+2)", "language": "python"}' | jq -r '.execution_id')

# 1초 대기 (워커가 처리할 시간)
sleep 1

# 결과 조회
curl http://localhost:8000/execution/$EXEC_ID

# 응답:
# {"status": "completed", "stdout": "4\n", "exit_code": "0", ...}
```

### 3.4 다중 워커 및 확장성

**난이도**: ⭐⭐⭐ (중간)

**문제**: 여러 워커를 실행하여 처리량 증가

**작업**:
1. 워커 ID 할당
2. 여러 워커 프로세스 동시 실행
3. 자동 스케일링 기본 로직

**코드 예시**:
```python
import multiprocessing
import os

def worker_process(worker_id: int):
    """각 워커 프로세스의 메인 함수"""
    print(f"Worker {worker_id} (PID: {os.getpid()}) started")

    while True:
        task_data = redis_client.blpop('codebeaker:queue', timeout=1)
        if task_data:
            # 작업 처리
            process_task(task_data, worker_id)

def start_worker_pool(num_workers: int = 4):
    """워커 풀 시작"""
    processes = []

    for i in range(num_workers):
        p = multiprocessing.Process(target=worker_process, args=(i,))
        p.start()
        processes.append(p)

    # 프로세스 대기
    for p in processes:
        p.join()

if __name__ == '__main__':
    num_workers = int(os.getenv('NUM_WORKERS', '4'))
    start_worker_pool(num_workers)
```

**실행**:
```bash
# 4개 워커로 시작
NUM_WORKERS=4 python worker.py
```

**Phase 3 마일스톤**:
- ✅ Redis 기반 작업 큐
- ✅ 비동기 실행 API
- ✅ 백그라운드 워커 프로세스
- ✅ 결과 조회 API
- ✅ 다중 워커 지원

---

## Phase 4: 보안 강화 (4-6주)

**목표**: 프로덕션 수준의 다층 보안 구현

### 4.1 네트워크 격리 강화

**난이도**: ⭐⭐⭐ (중간)

**문제**: 컨테이너의 외부 네트워크 접근 완전 차단

**작업**:
1. Docker 네트워크 정책 설정
2. iptables 규칙으로 외부 접근 차단
3. DNS 쿼리도 차단

**코드 예시**:
```python
# 컨테이너 실행 시 네트워크 완전 차단
container = client.containers.run(
    image,
    command=cmd,
    volumes=volumes,
    mem_limit='256m',
    nano_cpus=int(0.5 * 1e9),
    network_mode='none',  # 네트워크 완전 차단
    remove=True,
    detach=False
)
```

**검증**:
```python
code = """
import socket
try:
    socket.create_connection(('google.com', 80), timeout=2)
    print("FAIL: Network accessible")
except Exception as e:
    print(f"PASS: Network blocked - {type(e).__name__}")
"""
result = execute_python(code)
assert 'PASS' in result.stdout
```

### 4.2 파일시스템 격리

**난이도**: ⭐⭐⭐⭐ (복잡)

**문제**: 읽기 전용 루트 파일시스템 + 쓰기 가능 tmpfs

**Docker 설정**:
```python
container = client.containers.run(
    image,
    command=cmd,
    volumes={
        tmpdir: {'bind': '/code', 'mode': 'ro'}  # 읽기 전용
    },
    tmpfs={
        '/tmp': 'rw,noexec,nosuid,size=100m'  # 쓰기 가능, 실행 불가
    },
    read_only=True,  # 루트 파일시스템 읽기 전용
    security_opt=[
        'no-new-privileges:true'  # setuid/setgid 차단
    ],
    cap_drop=['ALL'],  # 모든 capabilities 제거
    remove=True
)
```

**검증**:
```python
# 루트에 쓰기 시도 (실패해야 함)
code = """
try:
    with open('/test.txt', 'w') as f:
        f.write('test')
    print("FAIL: Root writable")
except Exception as e:
    print(f"PASS: Root read-only - {type(e).__name__}")
"""
result = execute_python(code)
assert 'PASS' in result.stdout

# /tmp에 쓰기 시도 (성공해야 함)
code = """
try:
    with open('/tmp/test.txt', 'w') as f:
        f.write('test')
    print("PASS: /tmp writable")
except Exception as e:
    print(f"FAIL: /tmp not writable - {type(e).__name__}")
"""
result = execute_python(code)
assert 'PASS' in result.stdout
```

### 4.3 seccomp 프로필 적용

**난이도**: ⭐⭐⭐⭐⭐ (매우 복잡)

**문제**: 위험한 syscall 차단

**seccomp 프로필** (`seccomp-profile.json`):
```json
{
  "defaultAction": "SCMP_ACT_ERRNO",
  "architectures": ["SCMP_ARCH_X86_64"],
  "syscalls": [
    {
      "names": [
        "read", "write", "open", "close", "stat", "fstat",
        "lseek", "mmap", "mprotect", "munmap", "brk",
        "rt_sigaction", "rt_sigprocmask", "ioctl", "access",
        "pipe", "select", "sched_yield", "mremap", "msync",
        "dup", "dup2", "nanosleep", "getpid", "socket",
        "connect", "sendto", "recvfrom", "shutdown", "bind",
        "listen", "getsockname", "getpeername", "socketpair",
        "clone", "fork", "vfork", "execve", "exit", "wait4",
        "kill", "uname", "fcntl", "flock", "fsync",
        "getcwd", "chdir", "rename", "mkdir", "rmdir",
        "creat", "link", "unlink", "readlink", "chmod",
        "getdents", "getdents64", "getrlimit", "getrusage",
        "getuid", "getgid", "geteuid", "getegid",
        "setpgid", "getppid", "setsid", "getpgrp",
        "clock_gettime", "exit_group"
      ],
      "action": "SCMP_ACT_ALLOW"
    }
  ]
}
```

**적용**:
```python
container = client.containers.run(
    image,
    command=cmd,
    security_opt=[
        'seccomp=/path/to/seccomp-profile.json'
    ],
    ...
)
```

**검증**:
```python
# reboot syscall 시도 (차단되어야 함)
code = """
import ctypes
try:
    libc = ctypes.CDLL(None)
    libc.reboot(0x1234567)  # LINUX_REBOOT_CMD_RESTART
    print("FAIL: reboot allowed")
except Exception as e:
    print(f"PASS: reboot blocked - {type(e).__name__}")
"""
result = execute_python(code)
assert 'PASS' in result.stdout
```

### 4.4 AppArmor 프로필

**난이도**: ⭐⭐⭐⭐ (복잡)

**문제**: MAC (Mandatory Access Control) 추가

**AppArmor 프로필** (`/etc/apparmor.d/docker-codebeaker`):
```
#include <tunables/global>

profile docker-codebeaker flags=(attach_disconnected,mediate_deleted) {
  #include <abstractions/base>

  # 네트워크 차단
  deny network,

  # 파일 접근 제한
  deny /proc/sys/** rw,
  deny /sys/** rw,
  deny /proc/mem rw,

  # /tmp만 쓰기 허용
  /tmp/** rw,
  /code/** r,

  # 실행 파일
  /usr/bin/python* rix,
  /usr/bin/node* rix,
  /usr/bin/dotnet* rix,
}
```

**로드**:
```bash
sudo apparmor_parser -r -W /etc/apparmor.d/docker-codebeaker
```

**적용**:
```python
container = client.containers.run(
    image,
    command=cmd,
    security_opt=[
        'apparmor=docker-codebeaker'
    ],
    ...
)
```

**Phase 4 마일스톤**:
- ✅ 네트워크 완전 차단
- ✅ 읽기 전용 루트 + tmpfs
- ✅ seccomp syscall 필터링
- ✅ AppArmor MAC
- ✅ 모든 capabilities 제거

---

## Phase 5: 관찰성 및 모니터링 (3-4주)

**목표**: Prometheus, Grafana, Loki를 통한 메트릭 및 로그 수집

### 5.1 구조화된 로깅

**난이도**: ⭐⭐ (단순)

**문제**: JSON 형식의 일관된 로그 출력

**코드 예시**:
```python
import logging
import json

class JSONFormatter(logging.Formatter):
    def format(self, record):
        log_obj = {
            'timestamp': self.formatTime(record),
            'level': record.levelname,
            'service': 'worker',
            'message': record.getMessage(),
        }

        # 추가 컨텍스트
        if hasattr(record, 'execution_id'):
            log_obj['execution_id'] = record.execution_id
        if hasattr(record, 'language'):
            log_obj['language'] = record.language

        return json.dumps(log_obj)

# 설정
handler = logging.StreamHandler()
handler.setFormatter(JSONFormatter())
logger = logging.getLogger('codebeaker')
logger.addHandler(handler)
logger.setLevel(logging.INFO)

# 사용
logger.info('Execution started', extra={
    'execution_id': execution_id,
    'language': 'python'
})
```

**출력**:
```json
{"timestamp": "2025-01-15 10:30:45", "level": "INFO", "service": "worker", "message": "Execution started", "execution_id": "a1b2c3", "language": "python"}
```

### 5.2 Prometheus 메트릭

**난이도**: ⭐⭐⭐ (중간)

**문제**: 실행 메트릭을 Prometheus 형식으로 노출

**코드 예시**:
```python
from prometheus_client import Counter, Histogram, Gauge, start_http_server

# 메트릭 정의
executions_total = Counter(
    'codebeaker_executions_total',
    'Total number of code executions',
    ['language', 'status']
)

execution_duration = Histogram(
    'codebeaker_execution_duration_seconds',
    'Code execution duration in seconds',
    ['language'],
    buckets=[0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0]
)

queue_depth = Gauge(
    'codebeaker_queue_depth',
    'Number of pending executions in queue'
)

# 워커에서 메트릭 기록
def process_task(task):
    language = task['language']

    with execution_duration.labels(language=language).time():
        try:
            result = execute(task)
            executions_total.labels(language=language, status='success').inc()
        except Exception:
            executions_total.labels(language=language, status='failure').inc()
            raise

# 메트릭 서버 시작 (포트 8001)
start_http_server(8001)
```

**Prometheus 설정** (`prometheus.yml`):
```yaml
scrape_configs:
  - job_name: 'codebeaker-workers'
    static_configs:
      - targets: ['localhost:8001']
    scrape_interval: 15s
```

**검증**:
```bash
curl http://localhost:8001/metrics
# HELP codebeaker_executions_total Total number of code executions
# TYPE codebeaker_executions_total counter
codebeaker_executions_total{language="python",status="success"} 42
```

### 5.3 Grafana 대시보드

**난이도**: ⭐⭐⭐ (중간)

**작업**:
1. Grafana 설치 및 Prometheus 데이터소스 추가
2. 대시보드 생성

**주요 패널**:
```
1. 실행 성공률 (Stat):
   sum(rate(codebeaker_executions_total{status="success"}[5m])) /
   sum(rate(codebeaker_executions_total[5m])) * 100

2. P95 실행 시간 (Graph):
   histogram_quantile(0.95, rate(codebeaker_execution_duration_seconds_bucket[5m]))

3. 언어별 실행 분포 (Pie Chart):
   sum by (language) (codebeaker_executions_total)

4. 큐 깊이 (Graph):
   codebeaker_queue_depth
```

### 5.4 Loki 로그 집계

**난이도**: ⭐⭐⭐ (중간)

**Promtail 설정** (`promtail.yml`):
```yaml
server:
  http_listen_port: 9080

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://loki:3100/loki/api/v1/push

scrape_configs:
  - job_name: codebeaker
    static_configs:
      - targets:
          - localhost
        labels:
          job: codebeaker-worker
          __path__: /var/log/codebeaker/*.log
    pipeline_stages:
      - json:
          expressions:
            level: level
            execution_id: execution_id
            language: language
```

**LogQL 쿼리 예시**:
```
# 실행 실패 로그
{job="codebeaker-worker"} | json | level="ERROR"

# 특정 실행 ID
{job="codebeaker-worker"} | json | execution_id="a1b2c3d4"

# Python 실행만
{job="codebeaker-worker"} | json | language="python"
```

**Phase 5 마일스톤**:
- ✅ JSON 구조화 로깅
- ✅ Prometheus 메트릭 노출
- ✅ Grafana 대시보드
- ✅ Loki 로그 집계
- ✅ 알림 규칙 (Prometheus Alertmanager)

---

## Phase 6: 프로덕션 준비 (4-6주)

**목표**: Kubernetes 배포, CI/CD, 고가용성

### 6.1 Kubernetes 배포

**난이도**: ⭐⭐⭐⭐ (복잡)

**Deployment** (`k8s/worker-deployment.yaml`):
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: codebeaker-worker
spec:
  replicas: 3
  selector:
    matchLabels:
      app: codebeaker-worker
  template:
    metadata:
      labels:
        app: codebeaker-worker
    spec:
      containers:
      - name: worker
        image: codebeaker-worker:latest
        env:
        - name: REDIS_URL
          value: redis://redis:6379
        - name: NUM_WORKERS
          value: "2"
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
      # DinD (Docker-in-Docker) 사이드카
      - name: dind
        image: docker:dind
        securityContext:
          privileged: true
        volumeMounts:
        - name: docker-storage
          mountPath: /var/lib/docker
      volumes:
      - name: docker-storage
        emptyDir: {}
```

### 6.2 Horizontal Pod Autoscaler

**난이도**: ⭐⭐⭐ (중간)

**HPA** (`k8s/hpa.yaml`):
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: codebeaker-worker-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: codebeaker-worker
  minReplicas: 2
  maxReplicas: 20
  metrics:
  - type: External
    external:
      metric:
        name: redis_queue_depth
      target:
        type: AverageValue
        averageValue: "10"
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 30
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Pods
        value: 1
        periodSeconds: 120
```

### 6.3 CI/CD 파이프라인

**난이도**: ⭐⭐⭐ (중간)

**GitHub Actions** (`.github/workflows/ci.yml`):
```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    - name: Set up Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.11'

    - name: Install dependencies
      run: |
        pip install -r requirements.txt
        pip install -r requirements-dev.txt

    - name: Run tests
      run: pytest tests/ --cov=src/

    - name: Lint
      run: |
        black --check src/
        mypy src/

  build:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
    - uses: actions/checkout@v3

    - name: Build Docker image
      run: docker build -t codebeaker-worker:${{ github.sha }} .

    - name: Push to registry
      run: |
        echo "${{ secrets.DOCKER_PASSWORD }}" | docker login -u "${{ secrets.DOCKER_USERNAME }}" --password-stdin
        docker tag codebeaker-worker:${{ github.sha }} codebeaker-worker:latest
        docker push codebeaker-worker:${{ github.sha }}
        docker push codebeaker-worker:latest

  deploy:
    needs: build
    runs-on: ubuntu-latest
    steps:
    - name: Deploy to Kubernetes
      run: |
        kubectl set image deployment/codebeaker-worker worker=codebeaker-worker:${{ github.sha }}
        kubectl rollout status deployment/codebeaker-worker
```

**Phase 6 마일스톤**:
- ✅ Kubernetes 배포
- ✅ HPA 기반 자동 스케일링
- ✅ CI/CD 파이프라인
- ✅ 헬스 체크 및 롤링 업데이트
- ✅ 프로덕션 환경 운영

---

## Phase 7: 고급 격리 (Firecracker) (6-8주)

**목표**: Firecracker microVM을 통한 최고 수준의 격리

### 7.1 Firecracker 설치 및 테스트

**난이도**: ⭐⭐⭐⭐⭐ (매우 복잡)

**작업**:
1. Firecracker 바이너리 설치
2. 커널 및 루트 파일시스템 준비
3. 기본 microVM 시작 테스트

**설치**:
```bash
# Firecracker 다운로드
ARCH="$(uname -m)"
release_url="https://github.com/firecracker-microvm/firecracker/releases"
latest=$(basename $(curl -fsSLI -o /dev/null -w  %{url_effective} ${release_url}/latest))
curl -L ${release_url}/download/${latest}/firecracker-${latest}-${ARCH}.tgz \
| tar -xz

# KVM 확인
lsmod | grep kvm
```

### 7.2 Kata Containers 통합

**난이도**: ⭐⭐⭐⭐⭐ (매우 복잡)

**설정**:
```bash
# Kata Containers 설치
sudo apt-get install kata-containers

# containerd 설정
sudo tee -a /etc/containerd/config.toml <<EOF
[plugins."io.containerd.grpc.v1.cri".containerd.runtimes.kata]
  runtime_type = "io.containerd.kata.v2"
EOF

sudo systemctl restart containerd
```

**Kubernetes RuntimeClass**:
```yaml
apiVersion: node.k8s.io/v1
kind: RuntimeClass
metadata:
  name: kata-fc
handler: kata-fc
overhead:
  podFixed:
    memory: "200Mi"
    cpu: "100m"
```

**Pod 스펙**:
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: codebeaker-worker-kata
spec:
  runtimeClassName: kata-fc
  containers:
  - name: worker
    image: codebeaker-worker:latest
```

**Phase 7 마일스톤**:
- ✅ Firecracker microVM 실행
- ✅ Kata Containers 통합
- ✅ Kubernetes RuntimeClass 사용
- ✅ 스냅샷 및 복원 지원
- ✅ <200ms 콜드 스타트

---

## 진행 상황 추적

### 체크리스트

- [ ] **Phase 0**: 프로젝트 설정
  - [ ] 0.1 프로젝트 구조
  - [ ] 0.2 개발 도구
  - [ ] 0.3 Docker 환경

- [ ] **Phase 1**: MVP
  - [ ] 1.1 Hello World 실행기
  - [ ] 1.2 리소스 제한
  - [ ] 1.3 REST API
  - [ ] 1.4 Docker 격리

- [ ] **Phase 2**: 다중 언어
  - [ ] 2.1 런타임 추상화
  - [ ] 2.2 JavaScript 런타임
  - [ ] 2.3 C# 런타임
  - [ ] 2.4 의존성 설치

- [ ] **Phase 3**: 비동기 실행
  - [ ] 3.1 Redis 큐
  - [ ] 3.2 워커 구현
  - [ ] 3.3 결과 조회
  - [ ] 3.4 다중 워커

- [ ] **Phase 4**: 보안 강화
  - [ ] 4.1 네트워크 격리
  - [ ] 4.2 파일시스템 격리
  - [ ] 4.3 seccomp 프로필
  - [ ] 4.4 AppArmor 프로필

- [ ] **Phase 5**: 관찰성
  - [ ] 5.1 구조화 로깅
  - [ ] 5.2 Prometheus 메트릭
  - [ ] 5.3 Grafana 대시보드
  - [ ] 5.4 Loki 로그

- [ ] **Phase 6**: 프로덕션
  - [ ] 6.1 Kubernetes 배포
  - [ ] 6.2 HPA
  - [ ] 6.3 CI/CD
  - [ ] 6.4 운영 준비

- [ ] **Phase 7**: Firecracker
  - [ ] 7.1 Firecracker 설치
  - [ ] 7.2 Kata Containers

---

## 예상 일정

```
Phase 0: 프로젝트 설정         [ 1주 ] ████
Phase 1: MVP                [ 2-4주 ] ████████████
Phase 2: 다중 언어           [ 4-6주 ] ████████████████████
Phase 3: 비동기 실행          [ 3-4주 ] ████████████
Phase 4: 보안 강화           [ 4-6주 ] ████████████████████
Phase 5: 관찰성             [ 3-4주 ] ████████████
Phase 6: 프로덕션            [ 4-6주 ] ████████████████████
Phase 7: Firecracker       [ 6-8주 ] ████████████████████████████

총 예상 기간: 27-39주 (약 7-10개월)
```

---

## 다음 단계

1. **Phase 0**부터 시작: 프로젝트 구조 및 환경 설정
2. **단계별 검증**: 각 단계 완료 후 테스트 및 문서화
3. **점진적 개선**: 피드백을 바탕으로 반복 개선
4. **커뮤니티 참여**: 오픈소스 기여 및 피드백 수집

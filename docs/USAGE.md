# CodeBeaker 사용 가이드

## 🚀 빠른 시작

### 1. Docker 이미지 빌드

```powershell
# Windows
.\scripts\build-runtime-images.ps1

# Linux/Mac
./scripts/build-runtime-images.sh
```

### 2. API 서버 실행

**방법 1: 개발 환경**

```bash
# API 서버 실행 (WebSocket + JSON-RPC)
cd src/CodeBeaker.API
dotnet run
```

**방법 2: Docker Compose (프로덕션)**

```bash
docker-compose up -d
```

---

## 📡 WebSocket 연결

### 기본 URL
- Development: `ws://localhost:5000/ws/jsonrpc`
- Production: `ws://your-domain/ws/jsonrpc`

### WebSocket 클라이언트 예제

**JavaScript (브라우저)**:
```javascript
const ws = new WebSocket('ws://localhost:5000/ws/jsonrpc');

ws.onopen = () => {
  console.log('Connected to CodeBeaker');
};

ws.onmessage = (event) => {
  const response = JSON.parse(event.data);
  console.log('Response:', response);
};

ws.onerror = (error) => {
  console.error('WebSocket error:', error);
};

// JSON-RPC 요청 전송
function sendRequest(method, params, id = 1) {
  ws.send(JSON.stringify({
    jsonrpc: '2.0',
    id: id,
    method: method,
    params: params
  }));
}
```

**Node.js**:
```javascript
const WebSocket = require('ws');

const ws = new WebSocket('ws://localhost:5000/ws/jsonrpc');

ws.on('open', () => {
  // 세션 생성
  ws.send(JSON.stringify({
    jsonrpc: '2.0',
    id: 1,
    method: 'session.create',
    params: { language: 'python' }
  }));
});

ws.on('message', (data) => {
  const response = JSON.parse(data);
  console.log('Response:', response);
});
```

**Python**:
```python
import asyncio
import websockets
import json

async def codebeaker_client():
    uri = "ws://localhost:5000/ws/jsonrpc"
    async with websockets.connect(uri) as ws:
        # 세션 생성
        request = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "session.create",
            "params": {"language": "python"}
        }
        await ws.send(json.dumps(request))
        response = await ws.recv()
        print(json.loads(response))

asyncio.run(codebeaker_client())
```

---

## 🎯 세션 기반 실행 (권장)

세션 기반 실행은 컨테이너를 재사용하여 50-75% 성능 향상을 제공합니다.

### 1. 세션 생성

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.create",
  "params": {
    "language": "python",
    "idleTimeoutMinutes": 30,
    "maxLifetimeMinutes": 120,
    "memoryLimitMB": 512
  }
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "sessionId": "abc123def456",
    "containerId": "xyz789",
    "language": "python",
    "createdAt": "2025-10-27T12:00:00Z",
    "state": "Active",
    "config": {
      "idleTimeoutMinutes": 30,
      "maxLifetimeMinutes": 120
    }
  }
}
```

### 2. 파일 작성

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123def456",
    "command": {
      "type": "write_file",
      "path": "/workspace/hello.py",
      "content": "print('Hello, World!')",
      "mode": "Create"
    }
  }
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "success": true,
    "result": {
      "path": "/workspace/hello.py",
      "bytes": 22
    },
    "error": null,
    "durationMs": 45
  }
}
```

### 3. 코드 실행

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123def456",
    "command": {
      "type": "execute_shell",
      "commandName": "python3",
      "args": ["/workspace/hello.py"],
      "workingDirectory": "/workspace"
    }
  }
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "success": true,
    "result": {
      "exitCode": 0,
      "stdout": "Hello, World!\n",
      "stderr": "",
      "durationMs": 120
    },
    "error": null,
    "durationMs": 125
  }
}
```

### 4. 파일 읽기

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123def456",
    "command": {
      "type": "read_file",
      "path": "/workspace/hello.py"
    }
  }
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "success": true,
    "result": {
      "content": "print('Hello, World!')",
      "bytes": 22
    },
    "error": null,
    "durationMs": 35
  }
}
```

### 5. 세션 목록 조회

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "session.list",
  "params": {}
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "count": 1,
    "sessions": [
      {
        "sessionId": "abc123def456",
        "containerId": "xyz789",
        "language": "python",
        "createdAt": "2025-10-27T12:00:00Z",
        "lastActivity": "2025-10-27T12:15:30Z",
        "state": "Idle",
        "executionCount": 3,
        "idleMinutes": 2.5,
        "lifetimeMinutes": 15.5
      }
    ]
  }
}
```

### 6. 세션 종료

**요청**:
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "session.close",
  "params": {
    "sessionId": "abc123def456"
  }
}
```

**응답**:
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "sessionId": "abc123def456",
    "closed": true
  }
}
```

---

## 🔥 언어별 사용 예제

### Python 예제

```javascript
const ws = new WebSocket('ws://localhost:5000/ws/jsonrpc');

ws.onopen = async () => {
  // 1. 세션 생성
  ws.send(JSON.stringify({
    jsonrpc: '2.0',
    id: 1,
    method: 'session.create',
    params: { language: 'python' }
  }));
};

ws.onmessage = (event) => {
  const response = JSON.parse(event.data);

  if (response.id === 1) {
    // 세션 생성 완료
    const sessionId = response.result.sessionId;

    // 2. Python 코드 작성
    ws.send(JSON.stringify({
      jsonrpc: '2.0',
      id: 2,
      method: 'session.execute',
      params: {
        sessionId: sessionId,
        command: {
          type: 'write_file',
          path: '/workspace/script.py',
          content: 'for i in range(5):\n    print(f"Count: {i}")'
        }
      }
    }));
  } else if (response.id === 2) {
    // 파일 작성 완료, Python 실행
    const sessionId = 'abc123def456'; // 실제로는 저장된 값 사용

    ws.send(JSON.stringify({
      jsonrpc: '2.0',
      id: 3,
      method: 'session.execute',
      params: {
        sessionId: sessionId,
        command: {
          type: 'execute_shell',
          commandName: 'python3',
          args: ['/workspace/script.py']
        }
      }
    }));
  } else if (response.id === 3) {
    // 실행 완료
    console.log('Output:', response.result.result.stdout);
    // Output: Count: 0\nCount: 1\nCount: 2\nCount: 3\nCount: 4\n
  }
};
```

### JavaScript (Node.js) 예제

```javascript
// 1. 세션 생성
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.create",
  "params": { "language": "javascript" }
}

// 2. JavaScript 코드 작성
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "write_file",
      "path": "/workspace/app.js",
      "content": "const numbers = [1, 2, 3, 4, 5];\nconst sum = numbers.reduce((a, b) => a + b, 0);\nconsole.log(`Sum: ${sum}`);"
    }
  }
}

// 3. Node.js 실행
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "execute_shell",
      "commandName": "node",
      "args": ["/workspace/app.js"]
    }
  }
}
```

### Go 예제

```javascript
// 1. 세션 생성
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.create",
  "params": { "language": "go" }
}

// 2. Go 코드 작성
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "write_file",
      "path": "/workspace/main.go",
      "content": "package main\n\nimport \"fmt\"\n\nfunc main() {\n    fmt.Println(\"Hello from Go\")\n}"
    }
  }
}

// 3. Go 실행
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "execute_shell",
      "commandName": "go",
      "args": ["run", "/workspace/main.go"]
    }
  }
}
```

### C# (.NET) 예제

```javascript
// 1. 세션 생성
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.create",
  "params": { "language": "csharp" }
}

// 2. C# 코드 작성
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "write_file",
      "path": "/workspace/Program.cs",
      "content": "using System;\n\nclass Program\n{\n    static void Main()\n    {\n        Console.WriteLine(\"Hello from C#\");\n    }\n}"
    }
  }
}

// 3. C# 컴파일 및 실행
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "execute_shell",
      "commandName": "dotnet",
      "args": ["script", "/workspace/Program.cs"]
    }
  }
}
```

---

## 📦 Command 타입 상세

### 1. WriteFileCommand

**파일 작성 또는 생성**

```json
{
  "type": "write_file",
  "path": "/workspace/file.txt",
  "content": "파일 내용",
  "mode": "Create"
}
```

**모드**:
- `Create`: 새 파일 생성 (기존 파일 덮어쓰기)
- `Append`: 기존 파일에 추가

### 2. ReadFileCommand

**파일 읽기**

```json
{
  "type": "read_file",
  "path": "/workspace/file.txt"
}
```

### 3. ExecuteShellCommand

**Shell 명령 실행**

```json
{
  "type": "execute_shell",
  "commandName": "python3",
  "args": ["/workspace/script.py"],
  "workingDirectory": "/workspace"
}
```

### 4. CreateDirectoryCommand

**디렉토리 생성**

```json
{
  "type": "create_directory",
  "path": "/workspace/data"
}
```

### 5. CopyFileCommand

**파일 복사**

```json
{
  "type": "copy_file",
  "sourcePath": "/workspace/file1.txt",
  "destinationPath": "/workspace/file2.txt"
}
```

### 6. DeleteFileCommand

**파일 삭제**

```json
{
  "type": "delete_file",
  "path": "/workspace/file.txt"
}
```

### 7. ListDirectoryCommand

**디렉토리 목록 조회**

```json
{
  "type": "list_directory",
  "path": "/workspace"
}
```

---

## 🎬 실전 시나리오

### 시나리오 1: 멀티턴 대화 (파일시스템 상태 유지)

```javascript
// 1. 세션 생성
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 1,
  method: 'session.create',
  params: { language: 'python' }
}));

// 2. 첫 번째 코드 실행 (변수 정의)
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 2,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'write_file',
      path: '/workspace/data.py',
      content: 'x = 10\ny = 20'
    }
  }
}));

// 3. 두 번째 코드 실행 (변수 사용)
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 3,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'write_file',
      path: '/workspace/calc.py',
      content: 'from data import x, y\nprint(f"Sum: {x + y}")'
    }
  }
}));

// 4. 실행
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 4,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'execute_shell',
      commandName: 'python3',
      args: ['/workspace/calc.py']
    }
  }
}));
// Output: Sum: 30
```

### 시나리오 2: 패키지 설치 및 사용

```javascript
// 1. 세션 생성
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 1,
  method: 'session.create',
  params: { language: 'python' }
}));

// 2. pip 패키지 설치
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 2,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'execute_shell',
      commandName: 'pip',
      args: ['install', '--no-cache-dir', 'requests']
    }
  }
}));

// 3. 패키지 사용 코드 작성
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 3,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'write_file',
      path: '/workspace/api_test.py',
      content: 'import requests\nprint("requests version:", requests.__version__)'
    }
  }
}));

// 4. 실행
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 4,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'execute_shell',
      commandName: 'python3',
      args: ['/workspace/api_test.py']
    }
  }
}));
```

### 시나리오 3: 데이터 파일 처리

```javascript
// 1. CSV 데이터 작성
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 1,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'write_file',
      path: '/workspace/data.csv',
      content: 'name,age\nAlice,30\nBob,25\nCharlie,35'
    }
  }
}));

// 2. 데이터 처리 코드
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 2,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'write_file',
      path: '/workspace/process.py',
      content: `
import csv

with open('/workspace/data.csv', 'r') as f:
    reader = csv.DictReader(f)
    total_age = sum(int(row['age']) for row in reader)

print(f"Total age: {total_age}")
`
    }
  }
}));

// 3. 실행
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 3,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'execute_shell',
      commandName: 'python3',
      args: ['/workspace/process.py']
    }
  }
}));

// 4. 결과 파일 읽기
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 4,
  method: 'session.execute',
  params: {
    sessionId: sessionId,
    command: {
      type: 'read_file',
      path: '/workspace/data.csv'
    }
  }
}));
```

---

## ⚙️ 세션 설정 옵션

### SessionConfig 파라미터

```json
{
  "language": "python",
  "idleTimeoutMinutes": 30,
  "maxLifetimeMinutes": 120,
  "memoryLimitMB": 512,
  "cpuShares": 1024,
  "dockerImage": "codebeaker-python:latest"
}
```

**파라미터 설명**:
- `language`: 언어 선택 (python, javascript, go, csharp)
- `idleTimeoutMinutes`: 유휴 타임아웃 (기본: 30분)
- `maxLifetimeMinutes`: 최대 생명 주기 (기본: 120분)
- `memoryLimitMB`: 메모리 제한 (기본: 512MB)
- `cpuShares`: CPU 점유율 (기본: 1024)
- `dockerImage`: 커스텀 Docker 이미지 (선택 사항)

---

## 🔍 에러 처리

### 에러 응답 형식

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32600,
    "message": "Invalid Request",
    "data": "Additional error details"
  }
}
```

### 일반적인 에러 코드

| 코드 | 의미 | 해결 방법 |
|------|------|-----------|
| -32600 | Invalid Request | JSON-RPC 형식 확인 |
| -32601 | Method not found | 메서드 이름 확인 (session.create, session.execute 등) |
| -32602 | Invalid params | 파라미터 형식 확인 |
| -32603 | Internal error | 서버 로그 확인 |

### 명령 실행 에러

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "success": false,
    "result": null,
    "error": "File not found: /workspace/missing.py",
    "durationMs": 15
  }
}
```

---

## 📊 성능 특성

### 세션 기반 vs 단일 실행

```
단일 실행 (Stateless):
- Container create: ~200ms
- Code execution: 100-500ms
- Container cleanup: ~100ms
- Total: ~400-800ms

세션 실행 (Stateful):
- First execution: ~400ms (세션 생성)
- Subsequent: ~100-200ms (컨테이너 재사용)
- Improvement: 50-75% faster
```

### 최적화 팁

1. **세션 재사용**: 여러 실행이 필요한 경우 세션을 유지하세요
2. **배치 명령**: 여러 명령을 순차적으로 실행하여 왕복 시간 절약
3. **메모리 관리**: 필요한 만큼만 메모리 할당 (기본 512MB)
4. **세션 정리**: 사용 완료 후 session.close로 명시적 종료

---

## 🔧 문제 해결

### WebSocket 연결 실패

1. API 서버 실행 확인:
```bash
curl http://localhost:5000/health
```

2. WebSocket 엔드포인트 확인:
```bash
wscat -c ws://localhost:5000/ws/jsonrpc
```

### 세션 생성 실패

1. Docker 이미지 확인:
```bash
docker images | grep codebeaker
```

2. Docker 서비스 상태:
```bash
docker ps
docker info
```

### 명령 실행 실패

1. 세션 상태 확인:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.list",
  "params": {}
}
```

2. 컨테이너 로그 확인:
```bash
docker logs <container-id>
```

### 세션 자동 종료

세션은 다음 조건에서 자동 종료됩니다:
- IdleTimeout (기본 30분) 동안 활동이 없는 경우
- MaxLifetime (기본 120분) 초과

세션 유지 방법:
- 주기적으로 명령 실행
- IdleTimeout 값 조정
- session.list로 세션 상태 모니터링

---

## 🛡️ 보안 고려사항

1. **컨테이너 격리**: 각 세션은 독립된 Docker 컨테이너에서 실행
2. **네트워크 격리**: 기본적으로 네트워크 비활성화 (NetworkMode: "none")
3. **리소스 제한**: 메모리와 CPU 제한으로 DoS 방지
4. **자동 정리**: 유휴 세션 자동 종료로 리소스 관리
5. **파일시스템 격리**: /workspace 디렉토리 내부로 제한

---

## 📝 라이선스

MIT License - 자유롭게 사용, 수정, 배포 가능

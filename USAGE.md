# CodeBeaker 사용 가이드

## 🚀 빠른 시작

### 1. Docker 이미지 빌드

```powershell
# Windows
.\scripts\build-runtime-images.ps1

# Linux/Mac
./scripts/build-runtime-images.sh
```

### 2. API와 Worker 실행

**방법 1: 개발 환경 (수동 실행)**

```bash
# Terminal 1 - API 서버
cd src/CodeBeaker.API
dotnet run

# Terminal 2 - Worker 서비스
cd src/CodeBeaker.Worker
dotnet run
```

**방법 2: Docker Compose (프로덕션)**

```bash
docker-compose up -d
```

---

## 📡 API 사용법

### 기본 URL
- Development: `http://localhost:5039`
- Docker Compose: `http://localhost:5000`

### Swagger UI
브라우저에서 `http://localhost:5039`를 열면 대화형 API 문서를 볼 수 있습니다.

---

## 🔥 코드 실행 예제

### Python 실행

```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "for i in range(5):\n    print(f\"Count: {i}\")",
    "language": "python"
  }'
```

**응답**:
```json
{
  "executionId": "abc123...",
  "status": "pending",
  "createdAt": "2025-10-26T10:00:00Z"
}
```

### JavaScript 실행

```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "console.log(\"Hello from Node.js\")",
    "language": "javascript"
  }'
```

### Go 실행

```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "package main\nimport \"fmt\"\nfunc main() {\n    fmt.Println(\"Hello from Go\")\n}",
    "language": "go"
  }'
```

### C# 실행

```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "Console.WriteLine(\"Hello from C#\");",
    "language": "csharp"
  }'
```

---

## 📊 실행 결과 조회

### 상태 확인

```bash
curl http://localhost:5039/api/execution/{execution-id}
```

**응답** (완료된 경우):
```json
{
  "executionId": "abc123...",
  "status": "completed",
  "exitCode": 0,
  "stdout": "Count: 0\nCount: 1\nCount: 2\nCount: 3\nCount: 4\n",
  "stderr": "",
  "durationMs": 720,
  "timeout": false,
  "errorType": null,
  "createdAt": "2025-10-26T10:00:00Z",
  "completedAt": "2025-10-26T10:00:01Z"
}
```

---

## ⚙️ 고급 설정

### 실행 설정 옵션

```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "print(\"Hello\")",
    "language": "python",
    "config": {
      "timeout": 10,
      "memoryLimit": 256,
      "cpuLimit": 0.5,
      "disableNetwork": true,
      "readOnlyFilesystem": true,
      "packages": ["requests"]
    }
  }'
```

**설정 옵션**:
- `timeout`: 타임아웃 (초, 기본: 5)
- `memoryLimit`: 메모리 제한 (MB, 기본: 256)
- `cpuLimit`: CPU 제한 (코어 수, 기본: 0.5)
- `disableNetwork`: 네트워크 비활성화 (기본: true)
- `readOnlyFilesystem`: 읽기 전용 파일시스템 (기본: true)
- `packages`: 추가 패키지 목록

---

## 🌐 지원 언어 조회

```bash
curl http://localhost:5039/api/language
```

**응답**:
```json
[
  {
    "name": "python",
    "displayName": "Python",
    "version": "3.12",
    "aliases": ["python", "py"],
    "dockerImage": "codebeaker-python:latest"
  },
  {
    "name": "javascript",
    "displayName": "JavaScript (Node.js)",
    "version": "20",
    "aliases": ["javascript", "js", "node"],
    "dockerImage": "codebeaker-nodejs:latest"
  },
  {
    "name": "go",
    "displayName": "Go",
    "version": "1.21",
    "aliases": ["go", "golang"],
    "dockerImage": "codebeaker-golang:latest"
  },
  {
    "name": "csharp",
    "displayName": "C# (.NET)",
    "version": "8.0",
    "aliases": ["csharp", "cs", "dotnet"],
    "dockerImage": "codebeaker-dotnet:latest"
  }
]
```

---

## 🔧 문제 해결

### Worker가 작업을 처리하지 않는 경우

1. Worker 로그 확인:
```bash
docker logs codebeaker-worker
```

2. Docker 이미지 확인:
```bash
docker images | grep codebeaker
```

3. 큐 상태 확인:
```bash
# 큐 디렉토리 확인
ls /tmp/codebeaker-queue/pending/
ls /tmp/codebeaker-queue/processing/
```

### API가 응답하지 않는 경우

1. 헬스체크:
```bash
curl http://localhost:5039/health
```

2. API 로그 확인:
```bash
docker logs codebeaker-api
```

### Docker 이미지 재빌드

```bash
docker build -t codebeaker-python:latest docker/runtimes/python
docker build -t codebeaker-nodejs:latest docker/runtimes/nodejs
docker build -t codebeaker-golang:latest docker/runtimes/golang
docker build -t codebeaker-dotnet:latest docker/runtimes/csharp
```

---

## 📈 성능 최적화

### Worker 동시성 조정

`appsettings.json`:
```json
{
  "Worker": {
    "MaxConcurrency": 20,
    "PollIntervalSeconds": 0.5,
    "MaxRetries": 3
  }
}
```

### 리소스 제한 조정

실행 요청 시 `config` 설정:
```json
{
  "timeout": 30,
  "memoryLimit": 512,
  "cpuLimit": 1.0
}
```

---

## 🛡️ 보안 고려사항

1. **네트워크 격리**: 기본적으로 네트워크 비활성화
2. **읽기 전용 파일시스템**: 악의적인 파일 쓰기 방지
3. **리소스 제한**: CPU와 메모리 제한으로 DoS 방지
4. **타임아웃**: 무한 실행 방지
5. **비root 사용자**: Docker 컨테이너 내부에서 비root 사용자로 실행

---

## 📝 라이선스

MIT License - 자유롭게 사용, 수정, 배포 가능

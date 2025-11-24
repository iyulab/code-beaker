# 🛠️ CodeBeaker 개발자 가이드

로컬 환경에서 CodeBeaker를 개발하고 테스트하기 위한 완벽한 가이드입니다.

---

## 📋 목차

1. [사전 요구사항](#사전-요구사항)
2. [빠른 시작](#빠른-시작)
3. [단계별 설정](#단계별-설정)
4. [개발 워크플로우](#개발-워크플로우)
5. [테스트 방법](#테스트-방법)
6. [트러블슈팅](#트러블슈팅)
7. [개발 팁](#개발-팁)

---

## 사전 요구사항

### 필수 소프트웨어

1. **.NET 8.0 SDK**
   - 다운로드: https://dotnet.microsoft.com/download/dotnet/8.0
   - 확인: `dotnet --version` (8.0.x 이상)

2. **Docker Desktop**
   - Windows: https://docs.docker.com/desktop/install/windows-install/
   - Mac: https://docs.docker.com/desktop/install/mac-install/
   - Linux: https://docs.docker.com/desktop/install/linux-install/
   - 확인: `docker --version` 및 `docker info`

3. **Git**
   - 다운로드: https://git-scm.com/downloads
   - 확인: `git --version`

### 선택 사항

- **Visual Studio 2022** (Community 이상) 또는 **JetBrains Rider**
- **Postman** 또는 **Insomnia** (API 테스트용)
- **PowerShell 7+** (Windows, 권장)

---

## 빠른 시작

### 1️⃣ 자동 설정 (권장)

**Windows:**
```powershell
# 저장소 클론
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker

# 자동 설정 스크립트 실행 (5-10분 소요)
.\scripts\setup-local-dev.ps1

# 개발 서버 시작
.\scripts\start-dev.ps1
```

**Linux/Mac:**
```bash
# 저장소 클론
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker

# 스크립트 실행 권한 부여
chmod +x scripts/*.sh

# 자동 설정 스크립트 실행 (5-10분 소요)
./scripts/setup-local-dev.sh

# 개발 서버 시작 (수동)
# Terminal 1: cd src/CodeBeaker.API && dotnet run
# Terminal 2: cd src/CodeBeaker.Worker && dotnet run
```

### 2️⃣ 브라우저에서 확인

Swagger UI 접속: http://localhost:5039

### 3️⃣ API 테스트

**Windows:**
```powershell
.\scripts\test-examples.ps1
```

**Linux/Mac/Git Bash:**
```bash
# Health Check
curl http://localhost:5039/health

# Python 코드 실행
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "print(\"Hello from CodeBeaker!\")",
    "language": "python"
  }'
```

---

## 단계별 설정

자동 스크립트를 사용하지 않고 수동으로 설정하려면:

### Step 1: 프로젝트 빌드

```bash
# NuGet 패키지 복원
dotnet restore

# 프로젝트 빌드
dotnet build -c Debug

# 테스트 실행
dotnet test
```

**예상 결과:**
```
✅ CodeBeaker.Core.Tests: 14 passing, 1 skipped
✅ CodeBeaker.Runtimes.Tests: 22 passing
⏭️ CodeBeaker.Integration.Tests: 11 skipped (Docker 이미지 필요)
```

### Step 2: Docker 런타임 이미지 빌드

각 언어별 Docker 이미지를 빌드합니다 (5-10분 소요):

**Windows (PowerShell):**
```powershell
.\scripts\build-runtime-images.ps1
```

**Linux/Mac:**
```bash
./scripts/build-runtime-images.sh
```

**수동 빌드:**
```bash
docker build -t codebeaker-python:latest docker/runtimes/python
docker build -t codebeaker-nodejs:latest docker/runtimes/nodejs
docker build -t codebeaker-golang:latest docker/runtimes/golang
docker build -t codebeaker-dotnet:latest docker/runtimes/csharp
```

**확인:**
```bash
docker images | grep codebeaker
```

### Step 3: 큐/저장소 디렉토리 생성

**Windows:**
```powershell
$queuePath = "$env:TEMP\codebeaker-queue"
$storagePath = "$env:TEMP\codebeaker-storage"

New-Item -ItemType Directory -Path "$queuePath\pending" -Force
New-Item -ItemType Directory -Path "$queuePath\processing" -Force
New-Item -ItemType Directory -Path "$queuePath\completed" -Force
New-Item -ItemType Directory -Path $storagePath -Force
```

**Linux/Mac:**
```bash
mkdir -p /tmp/codebeaker-queue/{pending,processing,completed}
mkdir -p /tmp/codebeaker-storage
```

### Step 4: 서비스 실행

**Terminal 1 - API 서버:**
```bash
cd src/CodeBeaker.API
dotnet run
```

출력:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5039
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Terminal 2 - Worker 서비스:**
```bash
cd src/CodeBeaker.Worker
dotnet run
```

출력:
```
info: CodeBeaker.Worker.Worker[0]
      CodeBeaker Worker starting...
info: CodeBeaker.Worker.Worker[0]
      Worker polling for tasks...
```

---

## 개발 워크플로우

### 일반적인 개발 사이클

```
1. 코드 변경
   ↓
2. 빌드 (dotnet build)
   ↓
3. 테스트 실행 (dotnet test)
   ↓
4. 로컬 서버 재시작
   ↓
5. API 테스트
   ↓
6. 커밋 & 푸시
```

### IDE에서 개발하기

#### Visual Studio 2022

1. `CodeBeaker.sln` 열기
2. 시작 프로젝트 설정:
   - 우클릭 Solution → Properties
   - Multiple startup projects 선택
   - CodeBeaker.API: Start
   - CodeBeaker.Worker: Start
3. F5로 디버깅 시작

#### VS Code

1. `.vscode/launch.json` 추가:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (API)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/CodeBeaker.API/bin/Debug/net8.0/CodeBeaker.API.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/CodeBeaker.API",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "name": ".NET Core Launch (Worker)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/CodeBeaker.Worker/bin/Debug/net8.0/CodeBeaker.Worker.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/CodeBeaker.Worker"
    }
  ],
  "compounds": [
    {
      "name": "API + Worker",
      "configurations": [".NET Core Launch (API)", ".NET Core Launch (Worker)"]
    }
  ]
}
```

### Hot Reload 사용하기

코드 변경 시 자동으로 재시작:

```bash
dotnet watch run
```

---

## 테스트 방법

### 1. 단위 테스트

**모든 테스트 실행:**
```bash
dotnet test
```

**특정 프로젝트만:**
```bash
dotnet test tests/CodeBeaker.Core.Tests/
dotnet test tests/CodeBeaker.Runtimes.Tests/
```

**커버리지 포함:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html
# 결과: tests/*/coverage/index.html
```

### 2. API 테스트

#### Swagger UI (권장)

1. 브라우저에서 http://localhost:5039 열기
2. 각 API 엔드포인트 테스트
3. "Try it out" 버튼으로 실행

#### PowerShell 스크립트

```powershell
.\scripts\test-examples.ps1
```

4개 언어(Python, JavaScript, Go, C#)의 코드 실행을 자동 테스트합니다.

#### curl 명령어

**Health Check:**
```bash
curl http://localhost:5039/health
```

**지원 언어 조회:**
```bash
curl http://localhost:5039/api/language
```

**Python 코드 실행:**
```bash
curl -X POST http://localhost:5039/api/execution \
  -H "Content-Type: application/json" \
  -d '{
    "code": "for i in range(5):\n    print(f\"Count: {i}\")",
    "language": "python"
  }'
```

**실행 결과 조회:**
```bash
curl http://localhost:5039/api/execution/{execution-id}
```

#### Postman Collection

`postman_collection.json` 파일 import:

```json
{
  "info": {
    "name": "CodeBeaker API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Health Check",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/health"
      }
    },
    {
      "name": "Get Languages",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/language"
      }
    },
    {
      "name": "Execute Python",
      "request": {
        "method": "POST",
        "url": "{{baseUrl}}/api/execution",
        "header": [
          {
            "key": "Content-Type",
            "value": "application/json"
          }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"code\": \"print('Hello World')\",\n  \"language\": \"python\"\n}"
        }
      }
    },
    {
      "name": "Get Execution Result",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/execution/{{executionId}}"
      }
    }
  ],
  "variable": [
    {
      "key": "baseUrl",
      "value": "http://localhost:5039"
    },
    {
      "key": "executionId",
      "value": ""
    }
  ]
}
```

### 3. Integration 테스트

Docker 이미지 빌드 후:

```bash
dotnet test tests/CodeBeaker.Integration.Tests/
```

### 4. 성능 벤치마크

```bash
cd benchmarks/CodeBeaker.Benchmarks
dotnet run -c Release
```

---

## 트러블슈팅

### 문제 1: API 서버가 시작되지 않음

**증상:**
```
System.IO.IOException: Failed to bind to address http://127.0.0.1:5039: address already in use
```

**해결:**
```powershell
# Windows
Get-Process -Name dotnet | Stop-Process -Force

# Linux/Mac
pkill -9 dotnet
```

또는 다른 포트 사용:
```bash
cd src/CodeBeaker.API
ASPNETCORE_URLS="http://localhost:5040" dotnet run
```

### 문제 2: Worker가 작업을 처리하지 않음

**확인 사항:**

1. **Docker 이미지 존재 여부:**
```bash
docker images | grep codebeaker
```

2. **큐 디렉토리 상태:**
```bash
# Windows
dir $env:TEMP\codebeaker-queue\pending

# Linux/Mac
ls /tmp/codebeaker-queue/pending/
```

3. **Worker 로그 확인:**
Worker 터미널에서 "Worker polling for tasks..." 메시지 확인

### 문제 3: Docker 이미지 빌드 실패

**증상:**
```
ERROR: failed to solve: process "/bin/sh -c pip install ..." did not complete successfully
```

**해결:**
1. Docker Desktop이 실행 중인지 확인
2. 인터넷 연결 확인
3. Docker 캐시 정리:
```bash
docker system prune -a
```

### 문제 4: 테스트 실행 시 Docker 관련 오류

**증상:**
```
Docker.DotNet.DockerApiException: Docker API responded with status code=InternalServerError
```

**해결:**
1. Docker Desktop 실행 확인
2. Docker socket 권한 확인 (Linux):
```bash
sudo chmod 666 /var/run/docker.sock
```

### 문제 5: NuGet 복원 실패

**증상:**
```
error NU1101: Unable to find package
```

**해결:**
```bash
# NuGet 캐시 정리
dotnet nuget locals all --clear

# 재시도
dotnet restore
```

---

## 개발 팁

### 1. 로그 레벨 조정

`appsettings.json` 또는 `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "CodeBeaker": "Trace",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 2. 개발 환경 변수

`.env` 파일 생성 (gitignore됨):

```bash
ASPNETCORE_ENVIRONMENT=Development
Queue__Path=/tmp/codebeaker-queue
Storage__Path=/tmp/codebeaker-storage
Worker__MaxConcurrency=5
Worker__PollIntervalSeconds=0.5
```

### 3. 빠른 재빌드

변경된 프로젝트만:

```bash
dotnet build src/CodeBeaker.Core/ --no-dependencies
dotnet build src/CodeBeaker.API/ --no-dependencies
```

### 4. Docker 이미지 경량화

개발 중에는 이미지 크기를 줄이기 위해:

```dockerfile
# 멀티스테이지 빌드 대신 단일 스테이지 사용
FROM python:3.12-slim
# ... (개발용 간소화)
```

### 5. 디버그 포트 변경

충돌 방지를 위해:

```bash
# API
dotnet run --urls "http://localhost:5100"

# Worker (포트 불필요)
dotnet run
```

### 6. Watch 모드로 개발

파일 변경 시 자동 재시작:

```bash
cd src/CodeBeaker.API
dotnet watch run
```

### 7. 빠른 테스트 실행

특정 테스트만:

```bash
dotnet test --filter "FullyQualifiedName~FileQueueTests"
dotnet test --filter "Category=Fast"
```

### 8. 코드 커버리지 확인

```bash
dotnet test /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:Threshold=80
```

---

## 추가 리소스

### 문서
- [아키텍처 설계](docs/CSHARP_ARCHITECTURE.md)
- [마이그레이션 가이드](docs/MIGRATION.md)
- [사용자 가이드](USAGE.md)
- [완료 요약](docs/COMPLETION_SUMMARY.md)

### API 문서
- Swagger UI: http://localhost:5039
- OpenAPI Spec: http://localhost:5039/swagger/v1/swagger.json

### 커뮤니티
- GitHub Issues: https://github.com/iyulab/codebeaker/issues
- Discussions: https://github.com/iyulab/codebeaker/discussions

---

## 다음 단계

개발 환경이 준비되었다면:

1. **코드 탐색**: `src/CodeBeaker.Core/`부터 시작
2. **테스트 작성**: `tests/`에 새 테스트 추가
3. **기능 추가**: 새 런타임 또는 API 엔드포인트 구현
4. **문서 업데이트**: 변경사항을 문서에 반영
5. **PR 생성**: 기여를 공유하세요!

---

**Happy Coding! 🚀**

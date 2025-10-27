# 🧪 CodeBeaker

**고성능 다중 언어 코드 실행 플랫폼 with Session Management**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

---

## 🚀 개요

CodeBeaker는 **Docker 격리 환경**에서 다중 언어 코드를 안전하게 실행하는 고성능 플랫폼입니다.

### 핵심 특징

- **세션 기반 실행**: Stateful 컨테이너 재사용으로 50-75% 성능 향상
- **파일시스템 유지**: 멀티턴 대화 및 상태 보존 지원
- **WebSocket + JSON-RPC**: 실시간 양방향 통신
- **Custom Commands**: Shell 우회 Docker API 직접 호출 (20% 성능 개선)
- **Docker 격리**: 언어별 샌드박스 실행 환경
- **타입 안전**: .NET 8.0 컴파일 타임 검증

### ✅ 개발 현황

- ✅ **Phase 1**: JSON-RPC 2.0 + WebSocket 완료
- ✅ **Phase 2**: Custom Command Interface 완료
- ✅ **Phase 3**: Session Management 완료
- ✅ **통합 테스트**: 17개 테스트 완료
- ✅ **API 통합**: 프로덕션 준비 완료

**진행률**: ✅ **프로덕션 배포 가능** 🚀

---

## ⚡ 빠른 시작

### 사전 요구사항

- .NET 8.0 SDK
- Docker Desktop
- (선택) Visual Studio 2022 또는 JetBrains Rider

### 🎯 3단계 설정 (5분)

**Windows:**
```powershell
# 저장소 클론
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker

# 자동 설정 (Docker 이미지 빌드 포함, 5-10분 소요)
.\scripts\setup-local-dev.ps1

# 개발 서버 시작
.\scripts\start-dev.ps1
```

**Linux/Mac:**
```bash
# 저장소 클론
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker

# 자동 설정 (Docker 이미지 빌드 포함, 5-10분 소요)
chmod +x scripts/*.sh
./scripts/setup-local-dev.sh

# 개발 서버 시작
dotnet run --project src/CodeBeaker.API
```

### 🌐 WebSocket 연결

```
ws://localhost:5000/ws/jsonrpc
```

### 📝 사용 예제

```javascript
const ws = new WebSocket('ws://localhost:5000/ws/jsonrpc');

// 1. 세션 생성
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 1,
  method: 'session.create',
  params: { language: 'python' }
}));

// 2. 코드 실행
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 2,
  method: 'session.execute',
  params: {
    sessionId: 'abc123',
    command: {
      type: 'write_file',
      path: '/workspace/hello.py',
      content: 'print("Hello, World!")'
    }
  }
}));

// 3. 세션 종료
ws.send(JSON.stringify({
  jsonrpc: '2.0',
  id: 3,
  method: 'session.close',
  params: { sessionId: 'abc123' }
}));
```

---

## 🏗️ 아키텍처

### 시스템 구성

```
┌─────────────────────────────────────────┐
│          WebSocket Client               │
│         (JSON-RPC 2.0)                  │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│         CodeBeaker API                  │
│    (ASP.NET Core 8 + WebSocket)         │
│                                          │
│  ┌──────────────────────────────────┐   │
│  │   JSON-RPC Router                │   │
│  │  - session.create                │   │
│  │  - session.execute               │   │
│  │  - session.list                  │   │
│  │  - session.close                 │   │
│  └──────────────────────────────────┘   │
│                                          │
│  ┌──────────────────────────────────┐   │
│  │   Session Manager                │   │
│  │  - Container pooling             │   │
│  │  - Filesystem persistence        │   │
│  │  - Auto cleanup (30min idle)     │   │
│  └──────────────────────────────────┘   │
│                                          │
│  ┌──────────────────────────────────┐   │
│  │   Command Executor               │   │
│  │  - Docker API direct calls       │   │
│  │  - WriteFile, ReadFile, etc.     │   │
│  └──────────────────────────────────┘   │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│         Docker Engine                   │
│  - codebeaker-python:latest             │
│  - codebeaker-nodejs:latest             │
│  - codebeaker-golang:latest             │
│  - codebeaker-dotnet:latest             │
└─────────────────────────────────────────┘
```

### 지원 언어

| 언어       | 버전        | Docker 이미지           |
|-----------|------------|------------------------|
| Python    | 3.12       | codebeaker-python      |
| JavaScript| Node 20    | codebeaker-nodejs      |
| Go        | 1.21       | codebeaker-golang      |
| C#        | .NET 8     | codebeaker-dotnet      |

---

## 🎯 주요 기능

### 1. 세션 기반 실행

**컨테이너 재사용으로 50-75% 성능 향상**

```
단일 실행 (Stateless):
- Container create: ~200ms
- Code execution: 100-500ms
- Container cleanup: ~100ms
- Total: ~400-800ms

세션 실행 (Stateful):
- First execution: ~400ms (create session)
- Subsequent: ~100-200ms (컨테이너 재사용)
- Improvement: 50-75% faster
```

### 2. JSON-RPC 메서드

#### session.create - 세션 생성
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "session.create",
  "params": {
    "language": "python",
    "idleTimeoutMinutes": 30,
    "maxLifetimeMinutes": 120
  }
}
```

#### session.execute - 명령 실행
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "session.execute",
  "params": {
    "sessionId": "abc123",
    "command": {
      "type": "execute_shell",
      "commandName": "python3",
      "args": ["/workspace/script.py"]
    }
  }
}
```

#### session.list - 세션 목록
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "session.list",
  "params": {}
}
```

#### session.close - 세션 종료
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "session.close",
  "params": {
    "sessionId": "abc123"
  }
}
```

### 3. Command Types

- **WriteFileCommand**: 파일 작성
- **ReadFileCommand**: 파일 읽기
- **ExecuteShellCommand**: Shell 명령 실행
- **CreateDirectoryCommand**: 디렉토리 생성
- **CopyFileCommand**: 파일 복사
- **DeleteFileCommand**: 파일 삭제
- **ListDirectoryCommand**: 디렉토리 목록

---

## 🧪 테스트

### 통합 테스트 (17개)

```bash
# Session Manager 테스트
dotnet test --filter "FullyQualifiedName~SessionManagerTests"

# JSON-RPC 통합 테스트
dotnet test --filter "FullyQualifiedName~SessionJsonRpcTests"
```

### 모든 테스트 실행

```bash
dotnet test
```

---

## 📚 문서

### 핵심 문서
- [**사용자 가이드**](docs/USAGE.md) - WebSocket API 사용법 및 예제 ⭐
- [**프로덕션 준비 가이드**](docs/PRODUCTION_READY.md) - 배포 및 운영 가이드 ⭐
- [**아키텍처 설계**](docs/ARCHITECTURE.md) - 상세 시스템 설계
- [**개발 로드맵**](docs/TASKS.md) - Phase 1-3 완료 현황 및 향후 계획

### 개발 과정 문서
- [**개발 히스토리**](docs/DEVELOPMENT_HISTORY.md) - Phase 1-3 개발 과정
- [**개발자 가이드**](DEV_GUIDE.md) - 로컬 환경 설정 및 개발
- [**상세 문서**](docs/archive/) - 연구 문서, Phase 보고서, 마이그레이션 가이드

---

## 🎯 사용 사례

- **AI 에이전트**: LLM 생성 코드 안전 실행 + 멀티턴 대화
- **코딩 플랫폼**: 온라인 저지, 코드 채점
- **CI/CD**: 빌드 테스트 자동화
- **교육**: 학생 코드 실행 및 피드백
- **Jupyter-style Notebooks**: 상태 유지 실행 환경

---

## 🔧 개발

### 프로젝트 구조

```
CodeBeaker/
├── src/
│   ├── CodeBeaker.Commands/     # Command 타입 시스템
│   ├── CodeBeaker.Core/         # 핵심 라이브러리
│   │   ├── Docker/
│   │   ├── Sessions/           # Session Manager
│   │   └── Interfaces/
│   ├── CodeBeaker.JsonRpc/      # JSON-RPC 라우터
│   │   └── Handlers/           # Session 핸들러
│   ├── CodeBeaker.Runtimes/     # 언어별 런타임
│   ├── CodeBeaker.API/          # WebSocket API
│   └── CodeBeaker.Worker/       # 백그라운드 워커
├── tests/
│   ├── CodeBeaker.Core.Tests/
│   ├── CodeBeaker.Runtimes.Tests/
│   └── CodeBeaker.Integration.Tests/  # Session 통합 테스트
├── docker/
│   └── runtimes/                # 언어별 Dockerfile
├── docs/                         # Phase 완료 보고서
└── benchmarks/
    └── CodeBeaker.Benchmarks/   # 성능 벤치마크
```

### 벤치마크

```bash
cd benchmarks/CodeBeaker.Benchmarks
dotnet run -c Release
```

---

## 📈 성능 특성

### 세션 기반 실행
- **컨테이너 재사용**: 생성 오버헤드 제거
- **파일시스템 유지**: 반복 작업 시 속도 향상
- **자동 정리**: IdleTimeout (30분), MaxLifetime (120분)

### Command System
- **Shell 우회**: Docker API 직접 호출로 20% 성능 개선
- **Type-safe**: 7가지 Command 타입
- **Polymorphic**: JSON 직렬화/역직렬화

---

## 🤝 기여

기여를 환영합니다! 다음 단계를 따라주세요:

1. Fork 생성
2. Feature 브랜치 생성 (`git checkout -b feature/AmazingFeature`)
3. 변경사항 커밋 (`git commit -m 'Add AmazingFeature'`)
4. 브랜치 푸시 (`git push origin feature/AmazingFeature`)
5. Pull Request 생성

---

## 📄 라이선스

MIT License - [LICENSE](LICENSE) 파일 참조

---

## 🙏 감사

영감을 받은 프로젝트:
- [Judge0](https://github.com/judge0/judge0) - Isolate 샌드박싱
- [Piston](https://github.com/engineer-man/piston) - 경량 실행 엔진
- [E2B](https://e2b.dev/) - Firecracker 기반 실행
- [LSP](https://microsoft.github.io/language-server-protocol/) - JSON-RPC 프로토콜

---

**CodeBeaker - 안전하고 빠른 세션 기반 코드 실행 플랫폼** 🧪✨

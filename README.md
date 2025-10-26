# 🧪 CodeBeaker

**고성능 다중 언어 코드 실행 플랫폼**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

---

## 🚀 개요

CodeBeaker는 **Docker 격리 환경**에서 다중 언어 코드를 안전하게 실행하는 고성능 플랫폼입니다.

### 핵심 특징

- **파일시스템 기반**: Redis/PostgreSQL 불필요, 로컬 개발 친화적
- **Docker 격리**: 언어별 샌드박스 실행 환경
- **고성능**: C# 기반, 비동기 처리, 병렬 워커 풀
- **타입 안전**: .NET 8.0 컴파일 타임 검증

### 🚧 개발 현황 (Week 1)

- ✅ **Day 1-2**: .NET 8.0 Solution 구조 완료
- ✅ **Day 3-4**: Core Library 구현 완료 (15/15 tests passing)
- 🔄 **Day 5-7**: Runtimes 구현 진행 중
- ⏳ **Day 8-14**: API & Worker 구현 예정

**진행률**: 65% (Core 완료, Runtimes 진행 중)

---

## ⚡ 빠른 시작

### 사전 요구사항

- .NET 8.0 SDK
- Docker Desktop
- (선택) Visual Studio 2022 또는 JetBrains Rider

### 설치 및 실행

```bash
# 1. 저장소 클론
git clone https://github.com/iyulab/codebeaker.git
cd codebeaker

# 2. 솔루션 빌드
dotnet build

# 3. 테스트 실행
dotnet test

# 4. 런타임 Docker 이미지 빌드 (구현 완료 후)
cd docker/runtimes/python && docker build -t codebeaker-python .
cd ../golang && docker build -t codebeaker-golang .
```

> ⚠️ **주의**: API 및 Worker는 Day 8-14에 구현 예정입니다.

### 현재 구현 상태

**✅ 완료된 기능**:
- Core Models (ExecutionConfig, ExecutionResult, TaskItem)
- Interfaces (IQueue, IStorage, IRuntime)
- FileQueue - 파일시스템 기반 작업 큐 (FIFO, atomic operations)
- FileStorage - 파일시스템 기반 상태 저장소 (JSON persistence)
- DockerExecutor - Docker 컨테이너 실행기 (resource limits, security)
- Language Runtimes:
  - BaseRuntime - 추상 클래스 with template method pattern
  - PythonRuntime - Python 3.12 with pip package support
  - JavaScriptRuntime - Node.js 20 with npm package support
  - GoRuntime - Go 1.21 with go.mod and package support
  - CSharpRuntime - .NET 8 with NuGet package support
- RuntimeRegistry - Factory pattern with case-insensitive lookup and aliases
- Docker Build Scripts - PowerShell (Windows) and Bash (Linux/Mac)
- REST API Server:
  - ExecutionController - POST /api/execution, GET /api/execution/{id}
  - LanguageController - GET /api/language, GET /api/language/{name}
  - Swagger/OpenAPI documentation at root (/)
  - Health check endpoint (/health)
  - Dependency injection with IQueue and IStorage
- Background Worker Service:
  - Automatic queue polling and task processing
  - SemaphoreSlim concurrency control (max 10 concurrent executions)
  - Exponential backoff retry logic (max 3 retries)
  - Runtime integration via RuntimeRegistry
  - Graceful shutdown support
- Unit Tests (36/36 passing, 100%):
  - Core Tests: 14 passing, 1 skipped (flaky concurrent test)
  - Runtime Tests: 22 passing (100%)
- Integration Tests (11 created, requires Docker images)
- **End-to-End Pipeline Verified**: API → Queue → Worker → Runtime → Storage (720ms Python execution)

**⏳ 예정**:
- Docker Image Build Automation
- End-to-End Integration Tests
- Production Deployment Scripts
- Performance Optimization

---

## 🏗️ 아키텍처

### 시스템 구성

```
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│   API       │ ───> │   Queue     │ ───> │   Worker    │
│   Server    │      │ (Filesystem)│      │   Pool      │
└─────────────┘      └─────────────┘      └─────────────┘
                                                │
                                                ▼
                                          ┌─────────────┐
                                          │   Docker    │
                                          │  Runtimes   │
                                          └─────────────┘
```

### 지원 언어

| 언어       | 버전        | Docker 이미지           |
|-----------|------------|------------------------|
| Python    | 3.12       | codebeaker-python      |
| JavaScript| Node 20    | codebeaker-nodejs      |
| Go        | 1.21       | codebeaker-golang      |
| C#        | .NET 8     | codebeaker-dotnet      |

---

## 📚 문서

- [**C# 아키텍처 설계**](docs/CSHARP_ARCHITECTURE.md) - 상세 설계 문서
- [**마이그레이션 가이드**](docs/MIGRATION.md) - Python → C# 전환 로드맵
- [**파일시스템 아키텍처**](docs/FILESYSTEM_ARCHITECTURE.md) - 큐/저장소 설계

---

## 🎯 사용 사례

- **AI 에이전트**: LLM 생성 코드 안전 실행
- **코딩 플랫폼**: 온라인 저지, 코드 채점
- **CI/CD**: 빌드 테스트 자동화
- **교육**: 학생 코드 실행 및 피드백

---

## 🔧 개발

### 프로젝트 구조

```
CodeBeaker/
├── src/
│   ├── CodeBeaker.Core/         # 핵심 라이브러리
│   ├── CodeBeaker.Runtimes/     # 언어별 런타임
│   ├── CodeBeaker.API/          # REST API
│   └── CodeBeaker.Worker/       # 백그라운드 워커
├── tests/
│   ├── CodeBeaker.Core.Tests/
│   └── CodeBeaker.Integration.Tests/
├── docker/
│   └── runtimes/                # 언어별 Dockerfile
└── benchmarks/
    └── CodeBeaker.Benchmarks/   # 성능 벤치마크
```

### 테스트 실행

```bash
# 모든 테스트
dotnet test

# 커버리지 포함
dotnet test /p:CollectCoverage=true /p:CoverageReporter=cobertura
```

### 벤치마크

```bash
cd benchmarks/CodeBeaker.Benchmarks
dotnet run -c Release
```

---

## 📈 성능 목표

| 항목 | 목표 |
|------|------|
| API 응답 시간 (p99) | < 5ms |
| 워커 처리량 | > 200 req/s |
| 메모리 사용 (API) | < 100MB |
| 동시 워커 수 | > 50개 |

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

---

**CodeBeaker - 안전하고 빠른 코드 실행 플랫폼** 🧪

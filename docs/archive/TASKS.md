# CodeBeaker 개발 로드맵

> 📦 **Archived**. An early development roadmap, kept as a record of what was
> planned when. It is not the current roadmap — see [README.md](../../README.md#로드맵).

---

**작성 시점**: 2025

**주요 업데이트**:
- ✅ Phase 6-11 완료 (Observability, Documentation, Enhanced Runtimes, Package Management, Security)
- ✅ 5-layer 보안 아키텍처
- ✅ 147 tests (98.1% pass rate)
- ✅ npm, pip 패키지 관리 지원

---

## Legacy Document (Phase 5 Roadmap)

**이전 버전**: v1.2.0 (Phase 5 Complete)
**최종 업데이트**: 2025-10-27

---

## 📊 현재 상태

### ✅ 완료된 Phase (Phase 1-5)

#### Phase 1: JSON-RPC 2.0 + WebSocket Foundation
**목표**: 실시간 양방향 통신 기반 구축
**완료일**: 2025-10-27

**주요 성과**:
- ✅ JSON-RPC 2.0 Core Library
- ✅ WebSocket Transport Layer
- ✅ Streaming Execution Engine
- ✅ API 호환성 유지 (REST + JSON-RPC)

#### Phase 2: Custom Command Interface
**목표**: Shell 우회 직접 API 호출로 20% 성능 개선
**완료일**: 2025-10-27

**주요 성과**:
- ✅ Command 타입 시스템 (7가지 타입)
- ✅ Command Executor (Docker API 직접 호출)
- ✅ Runtime Adapter 리팩토링
- ✅ JSON-RPC Method 통합

**Command Types**:
- WriteFileCommand, ReadFileCommand
- ExecuteShellCommand, CreateDirectoryCommand
- CopyFileCommand, DeleteFileCommand, ListDirectoryCommand

#### Phase 3: Session Management
**목표**: Stateful execution으로 50-75% 성능 향상
**완료일**: 2025-10-27

**주요 성과**:
- ✅ Session Model & Manager
- ✅ Container Pooling 및 재사용
- ✅ Idle Timeout & Cleanup (30min/120min)
- ✅ JSON-RPC Session Methods (4개)

**JSON-RPC Methods**:
- session.create, session.execute
- session.list, session.close

#### Phase 4: Multi-Runtime Architecture
**목표**: 언어별 최적 런타임 자동 선택
**완료일**: 2025-10-27

**주요 성과**:
- ✅ Docker Runtime (Python, Node.js, Go, C#)
- ✅ Deno Runtime (JavaScript/TypeScript, 80ms 시작)
- ✅ Bun Runtime (JavaScript/TypeScript, 50ms 시작)
- ✅ RuntimeSelector (4가지 선택 전략)
- ✅ 통합 테스트 17/17 통과

**Runtime Capabilities**:
```
Docker: 격리 9/10, 시작 ~560ms*, 메모리 250MB
Deno:   격리 7/10, 시작 80ms,   메모리 30MB
Bun:    격리 7/10, 시작 50ms,   메모리 25MB
```
`*` 실제 측정 결과 (명시된 2000ms보다 72% 빠름)

**Selection Strategies**:
- Speed: Bun > Deno > Docker
- Security: Docker > Deno = Bun
- Memory: Bun > Deno > Docker
- Balanced: 종합 점수 기반

#### Phase 5: Performance Optimization & Benchmarking
**목표**: 실제 성능 측정 및 최적화 검증
**완료일**: 2025-10-27

**주요 성과**:
- ✅ 벤치마크 인프라 구축 (3가지 도구)
- ✅ 실제 성능 데이터 수집
- ✅ 성능 병목 지점 식별
- ✅ 성능 보고서 작성

**실제 성능 측정 결과**:
```
Docker Environment Creation: 562ms avg (72% faster than spec)
Code Execution: 1.2ms avg (sub-millisecond)
File Operations: 146ms avg (optimization opportunity)
RuntimeSelector: <100μs overhead (negligible)
```

**주요 인사이트**:
1. Docker 성능이 스펙보다 우수 (2000ms → 562ms)
2. 파일 작업이 주요 병목 (Docker 146ms vs Deno/Bun 예상 <5ms)
3. RuntimeSelector 오버헤드 무시 가능 (<0.1ms)
4. 네이티브 런타임 이점: 시작 11-40배, 파일 작업 30배 빠름

**벤치마크 도구**:
- RuntimeBenchmarks.cs (BenchmarkDotNet 기반)
- RuntimeSelectorBenchmarks.cs (알고리즘 성능)
- SimplePerformanceTest.cs (Stopwatch 기반 간단 측정)

---

## 📈 달성된 목표 요약

| 목표 | Phase | 상태 | 성과 |
|------|-------|------|------|
| **실시간 스트리밍** | 1 | ✅ | WebSocket 실시간 stdout/stderr |
| **성능 개선** | 2 | ✅ | Custom commands 20% 향상 |
| **상태 관리** | 3 | ✅ | Session 재사용 50-75% 향상 |
| **표준 프로토콜** | 1 | ✅ | JSON-RPC 2.0 준수 |
| **타입 안전성** | 2 | ✅ | 7가지 typed commands |
| **Multi-Runtime** | 4 | ✅ | 3개 런타임, 자동 선택 |
| **성능 검증** | 5 | ✅ | 실제 벤치마크 완료 |

---

## 🎯 선택적 고급 기능 (Phase 6+)

### 🟡 권장 우선순위

#### Option 1: 고급 기능 (추천 ⭐)
**예상 기간**: 2-3주

**구현 항목**:
- Rate limiting & throttling
- Execution history & audit logs
- Cost estimation & resource quotas
- Package installation support (npm/pip/gem)

**장점**:
- 사용자 대면 기능으로 즉시 가치 제공
- 프로덕션 환경에서 실용적
- Multi-Runtime 장점 극대화

#### Option 2: 보안 강화
**예상 기간**: 3-4주

**구현 항목**:
- Resource limits enforcement (CPU/메모리/디스크)
- Network isolation controls
- Code scanning & vulnerability detection
- Secrets management

**장점**:
- 멀티테넌트 환경 필수
- 규정 준수(compliance) 충족
- 프로덕션 신뢰성 향상

#### Option 3: 프로덕션 준비
**예상 기간**: 2-3주

**구현 항목**:
- Health checks & monitoring
- Graceful shutdown & cleanup
- Error recovery & retry logic
- Production deployment guide
- Kubernetes manifests

**장점**:
- 실제 배포 가능 상태
- 운영 안정성 확보
- DevOps 통합 준비

#### Option 4: 개발자 경험
**예상 기간**: 3-4주

**구현 항목**:
- CLI tool for local testing
- VS Code extension
- Swagger UI enhancements
- Example projects & tutorials

**장점**:
- 플랫폼 채택률 향상
- 온보딩 시간 단축
- 커뮤니티 성장

#### Option 5: 테스트 및 품질
**예상 기간**: 2-3주

**구현 항목**:
- E2E integration tests
- Load testing & stress tests
- Security penetration testing
- Cross-platform validation (Linux/Mac)

**장점**:
- 품질 보증 수준 향상
- 프로덕션 안정성 검증
- 회귀 방지

### 🟢 향후 고려 사항

#### Multi-Channel Architecture
**목표**: Control/Data/Status 채널 분리
**우선순위**: 낮음 (현재 단일 WebSocket로 충분)

**구현 항목**:
- Control channel (명령 전송)
- Data channel (대용량 파일 전송)
- Status channel (상태 알림)

#### Capabilities Negotiation
**목표**: 클라이언트-서버 기능 협상
**우선순위**: 낮음 (현재 고정 기능 세트로 충분)

**구현 항목**:
- Capability 모델
- Initialize handshake (LSP 스타일)
- Feature gating

#### Advanced Security (gVisor/Firecracker)
**목표**: 커널 수준 격리 강화
**우선순위**: 낮음 (Docker 격리로 충분)

**구현 항목**:
- gVisor runtime integration
- Firecracker MicroVM (PoC)
- 성능 벤치마크

#### Debug Adapter Protocol (DAP)
**목표**: 코드 디버깅 지원
**우선순위**: 낮음 (특정 사용 사례에만 필요)

**구현 항목**:
- DAP server implementation
- 언어별 debugger adapter (pdb, node --inspect, vsdbg)
- VS Code integration

---

## 🚀 다음 단계 권장사항

### ✅ 현재 상태
- **프로덕션 준비 완료**: Phase 1-5 완료로 실제 배포 가능
- **17/17 통합 테스트 통과**: 안정성 검증 완료
- **3개 런타임 지원**: Docker (사용 가능), Deno/Bun (설치 시 자동 활성화)
- **성능 검증 완료**: 실제 벤치마크 데이터 확보

### 🎯 권장 순서

**단기 (1-2개월)**:
1. Option 1 (고급 기능) - 사용자 가치 제공
2. Option 3 (프로덕션 준비) - 배포 및 운영 안정화

**중기 (3-4개월)**:
3. Option 2 (보안 강화) - 프로덕션 강건성
4. Option 5 (테스트 및 품질) - 품질 보증

**장기 (6개월+)**:
5. Option 4 (개발자 경험) - 생태계 확장
6. 고급 기능 (Multi-Channel, Capabilities, gVisor, DAP) - 특정 요구사항 발생 시

### 📋 즉시 실행 가능한 옵션

#### 배포 및 운영 시작
```bash
# Docker Compose 배포
docker-compose up -d

# Kubernetes 배포
kubectl apply -f k8s/

# 로컬 개발 환경
dotnet run --project src/CodeBeaker.API
```

#### Deno/Bun 런타임 활성화
```bash
# Deno 설치 (Windows)
irm https://deno.land/install.ps1 | iex

# Bun 설치 (Windows)
irm bun.sh/install.ps1 | iex

# Deno 설치 (Linux/Mac)
curl -fsSL https://deno.land/install.sh | sh

# Bun 설치 (Linux/Mac)
curl -fsSL https://bun.sh/install | bash
```

#### 성능 벤치마크 재실행
```bash
cd benchmarks/PerfTest
dotnet run -c Release
```

---

## 📚 문서 구조

### 핵심 문서
- **README.md**: 프로젝트 개요 및 빠른 시작 (Phase 5 반영)
- **docs/ARCHITECTURE.md**: 시스템 아키텍처 상세
- **docs/USAGE.md**: 사용자 가이드 및 API 예제
- **docs/PRODUCTION_READY.md**: 프로덕션 배포 가이드
- **docs/TASKS.md**: 개발 로드맵 (이 문서, Phase 5 반영)
- **claudedocs/PERFORMANCE_BENCHMARK_REPORT.md**: 성능 벤치마크 상세 보고서

### 개발 참고 문서
- **DEV_GUIDE.md**: 로컬 환경 설정 및 개발 가이드
- **docs/archive/**: Phase별 완료 보고서 및 연구 문서
  - PHASE4_COMPLETE.md
  - DEVELOPMENT_HISTORY.md
  - LIGHTWEIGHT_RUNTIME_RESEARCH.md
  - VERIFICATION_RESULTS.md
- **claudedocs/archive/**: 개발 과정 상세 문서
  - PHASE4_MULTIRUNTIME_IMPLEMENTATION_COMPLETE.md
  - BUN_RUNTIME_ADDITION_COMPLETE.md

---

## 🎓 핵심 학습 사항

### 성능 최적화
- ✅ **Custom commands > Raw shell**: 20% 성능 개선 (검증 완료)
- ✅ **WebSocket streaming > Polling**: 실시간성 향상
- ✅ **Session reuse > New container**: 50-75% 성능 향상 (검증 완료)
- ✅ **Native runtimes > Docker**: JavaScript/TypeScript 11-40배 빠른 시작

### 프로토콜 표준화
- ✅ **JSON-RPC 2.0**: LSP, DAP, Jupyter 공통 기반
- ✅ **WebSocket**: 양방향 실시간 통신
- ✅ **타입 안전성**: 7가지 Command types

### Multi-Runtime 전략
- ✅ **자동 선택**: RuntimePreference 기반 최적 런타임
- ✅ **성능 프로파일**: Speed, Security, Memory, Balanced
- ✅ **확장 가능**: 새 런타임 추가 용이 (IExecutionRuntime 구현)

### 실무 인사이트
- ✅ **Docker 성능**: 실제 스펙보다 우수 (2000ms → 562ms)
- ✅ **파일 작업 병목**: 네이티브 런타임으로 30배 개선 가능
- ✅ **RuntimeSelector 효율**: <100μs 오버헤드, 무시 가능
- ✅ **E2B (Firecracker)**: 참고용, 현재 Docker로 충분

---

## ✅ 체크리스트 (진행 상황)

### Phase 1: JSON-RPC + WebSocket ✅
- ✅ JSON-RPC 2.0 core library
- ✅ WebSocket transport layer
- ✅ Streaming execution engine
- ✅ Dual protocol support (REST + JSON-RPC)

### Phase 2: Custom Commands ✅
- ✅ Command type system (7 types)
- ✅ Command executor (Docker API direct)
- ✅ Runtime adapter refactoring (4 languages)
- ✅ Pattern matching dispatch

### Phase 3: Session Management ✅
- ✅ Session model and manager
- ✅ Container pooling
- ✅ Idle timeout and cleanup
- ✅ JSON-RPC session methods (4 methods)

### Phase 4: Multi-Runtime Architecture ✅
- ✅ Docker Runtime (Python, Node.js, Go, C#)
- ✅ Deno Runtime (JavaScript/TypeScript)
- ✅ Bun Runtime (JavaScript/TypeScript)
- ✅ RuntimeSelector (4 strategies)
- ✅ 통합 테스트 17/17

### Phase 5: Performance Optimization ✅
- ✅ 벤치마크 인프라 (3 tools)
- ✅ 실제 성능 측정
- ✅ 병목 지점 식별
- ✅ 성능 보고서 작성

### Phase 6+: 선택적 고급 기능 ⏳
- ⏳ Option 1: 고급 기능
- ⏳ Option 2: 보안 강화
- ⏳ Option 3: 프로덕션 준비
- ⏳ Option 4: 개발자 경험
- ⏳ Option 5: 테스트 및 품질

---

**문서 버전**: 3.0
**최종 업데이트**: 2025-10-27
**상태**: ✅ **v1.2.0 프로덕션 준비 완료** (Phase 5 Complete) 🚀

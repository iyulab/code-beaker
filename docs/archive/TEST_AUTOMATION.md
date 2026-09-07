# 🤖 CodeBeaker 테스트 자동화 가이드

CodeBeaker의 완전 자동화된 테스트 시스템 가이드입니다.

---

## 📋 목차

1. [테스트 자동화 개요](#테스트-자동화-개요)
2. [로컬 테스트 자동화](#로컬-테스트-자동화)
3. [CI/CD 파이프라인](#cicd-파이프라인)
4. [테스트 커버리지](#테스트-커버리지)
5. [지속적 개선](#지속적-개선)

---

## 테스트 자동화 개요

### 테스트 계층

```
┌─────────────────────────────────────────────┐
│  Integration Tests (11)                     │
│  - API 엔드포인트 테스트                    │
│  - End-to-End 워크플로우                    │
│  - Docker 런타임 검증                       │
└─────────────────────────────────────────────┘
                    ▲
┌─────────────────────────────────────────────┐
│  Unit Tests (36)                            │
│  - Core: FileQueue, FileStorage (14)        │
│  - Runtimes: 4개 언어 런타임 (22)           │
└─────────────────────────────────────────────┘
```

### 자동화 도구

1. **로컬 자동화**
   - `run-all-tests.ps1`: 전체 테스트 실행
   - `test-watch.ps1`: 파일 변경 감지 자동 테스트
   - `test-examples.ps1`: API 통합 테스트

2. **CI/CD 자동화**
   - GitHub Actions: 푸시/PR 시 자동 실행
   - Docker 빌드 자동화
   - 코드 품질 검사

3. **커버리지 리포트**
   - Codecov 통합
   - HTML 리포트 생성
   - 트렌드 추적

---

## 로컬 테스트 자동화

### 1. 전체 테스트 실행

**기본 실행:**
```powershell
.\scripts\run-all-tests.ps1
```

**커버리지 포함:**
```powershell
.\scripts\run-all-tests.ps1 -WithCoverage
```

**커버리지 리포트 생성:**
```powershell
.\scripts\run-all-tests.ps1 -WithCoverage -GenerateReport
```

**Integration 테스트 스킵:**
```powershell
.\scripts\run-all-tests.ps1 -SkipIntegration
```

**출력 예시:**
```
========================================
CodeBeaker 테스트 자동화
========================================

1. Core 단위 테스트 실행 중...
   ✅ Core 테스트 통과

2. Runtime 단위 테스트 실행 중...
   ✅ Runtime 테스트 통과

3. Integration 테스트 실행 중...
   Docker 이미지 확인 중...
   ✅ Integration 테스트 통과

4. 커버리지 리포트 생성 중...
   ✅ 커버리지 리포트 생성 완료
   리포트 위치: ./TestResults/CoverageReport/index.html

========================================
테스트 결과 요약
========================================

✅ Core Tests: Passed
✅ Runtime Tests: Passed
✅ Integration Tests: Passed
📊 Coverage Report: Generated

✅ 모든 테스트 통과!
```

### 2. Watch 모드 (개발 중 자동 테스트)

**Core 테스트만 감시:**
```powershell
.\scripts\test-watch.ps1 -Target Core
```

**Runtime 테스트만 감시:**
```powershell
.\scripts\test-watch.ps1 -Target Runtime
```

**모든 테스트 감시:**
```powershell
.\scripts\test-watch.ps1 -Target All
```

**동작 방식:**
1. 스크립트 실행 시 초기 테스트 실행
2. `.cs` 파일 변경 감지
3. 2초 디바운스 후 자동 테스트 실행
4. 결과 즉시 표시

**출력 예시:**
```
========================================
CodeBeaker Test Watch 모드
========================================

타겟: Core
파일 변경 감지 시 자동으로 테스트가 실행됩니다.
종료하려면 Ctrl+C를 누르세요.

초기 테스트 실행 중...

========================================
변경 감지: D:\code-beaker\src\CodeBeaker.Core\Queue\FileQueue.cs
14:32:15
========================================

🧪 CodeBeaker.Core.Tests 테스트 실행 중...
   ✅ CodeBeaker.Core.Tests 통과

대기 중... (파일 변경 감지)
```

### 3. 특정 테스트만 실행

**Core 테스트:**
```powershell
dotnet test tests/CodeBeaker.Core.Tests/ --filter "FullyQualifiedName~FileQueueTests"
```

**Runtime 테스트:**
```powershell
dotnet test tests/CodeBeaker.Runtimes.Tests/ --filter "FullyQualifiedName~PythonRuntimeTests"
```

**특정 테스트 메서드:**
```powershell
dotnet test --filter "FullyQualifiedName~SubmitTask_ShouldCreatePendingFile"
```

### 4. 병렬 테스트 실행

**최대 병렬도:**
```powershell
dotnet test --parallel
```

**병렬도 제한:**
```powershell
dotnet test --parallel --max-cpus 4
```

---

## CI/CD 파이프라인

### GitHub Actions 워크플로우

파일: `.github/workflows/ci.yml`

#### 트리거 조건

- **Push**: `main`, `develop` 브랜치에 푸시
- **Pull Request**: `main`, `develop` 브랜치로 PR
- **Manual**: GitHub UI에서 수동 실행

#### Job 구성

**Job 1: Build and Unit Tests**
```yaml
- Checkout code
- Setup .NET 8.0
- Cache NuGet packages
- Restore dependencies
- Build solution (Release)
- Run Core unit tests
- Run Runtime unit tests
- Upload test results (.trx)
- Upload coverage reports
- Publish to Codecov
```

**Job 2: Build Docker Images** (main 브랜치만)
```yaml
- Checkout code
- Setup Docker Buildx
- Cache Docker layers
- Build runtime images (4개 언어)
```

**Job 3: Integration Tests** (main 브랜치만, Docker 이미지 사용)
```yaml
- Checkout code
- Setup .NET 8.0
- Build all Docker images
- Run integration tests
- Upload test results
```

**Job 4: Code Quality Analysis**
```yaml
- Checkout code
- Setup .NET 8.0
- Build solution
- Run dotnet format (code formatting check)
```

**Job 5: Security Scan**
```yaml
- Checkout code
- Run Trivy vulnerability scanner
- Upload SARIF results to GitHub Security
```

**Job 6: Performance Benchmarks** (main 브랜치만)
```yaml
- Checkout code
- Setup .NET 8.0
- Run BenchmarkDotNet benchmarks
- Upload benchmark results
```

**Job 7: Prepare Release** (태그 푸시 시)
```yaml
- Checkout code
- Build release packages
- Create release archive
- Create GitHub Release with artifacts
```

### CI/CD 워크플로우 실행 시간

| Job | 평균 시간 | 의존성 |
|-----|----------|--------|
| Build and Unit Tests | ~3분 | None |
| Build Docker Images | ~8분 | Build and Unit Tests |
| Integration Tests | ~5분 | Build Docker Images |
| Code Quality | ~2분 | Build and Unit Tests |
| Security Scan | ~2분 | Build and Unit Tests |
| Benchmarks | ~3분 | Build and Unit Tests |

**총 소요 시간**: ~15분 (병렬 실행)

### CI 상태 확인

**브랜치별 상태:**
```powershell
# GitHub CLI 사용
gh run list --branch main

# 최신 실행 상태
gh run view
```

**실패 시 대응:**
1. GitHub Actions 탭에서 실패한 Job 확인
2. 로그 다운로드 및 분석
3. 로컬에서 재현 및 수정
4. Push 또는 PR 업데이트

---

## 테스트 커버리지

### 커버리지 측정

**로컬에서 커버리지 측정:**
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

**HTML 리포트 생성:**
```powershell
# ReportGenerator 설치 (최초 1회)
dotnet tool install -g dotnet-reportgenerator-globaltool

# 리포트 생성
reportgenerator `
    -reports:"TestResults/**/coverage.cobertura.xml" `
    -targetdir:"TestResults/CoverageReport" `
    -reporttypes:"Html;Cobertura"

# 리포트 열기
start TestResults/CoverageReport/index.html
```

### 커버리지 목표

| 프로젝트 | 목표 | 현재 |
|---------|------|------|
| CodeBeaker.Core | 85% | ~90% |
| CodeBeaker.Runtimes | 80% | ~85% |
| CodeBeaker.API | 75% | ~70% |
| CodeBeaker.Worker | 75% | ~65% |

### Codecov 통합

**자동 업로드** (GitHub Actions):
- 모든 PR에서 커버리지 측정
- Codecov에 자동 업로드
- PR에 커버리지 변경 코멘트

**수동 업로드**:
```powershell
# Codecov CLI 설치
choco install codecov

# 업로드
codecov -f "TestResults/**/coverage.cobertura.xml" -t $env:CODECOV_TOKEN
```

### 커버리지 리포트 읽기

**HTML 리포트 구조:**
```
CoverageReport/
├── index.html              # 전체 요약
├── Summary.html            # 상세 요약
├── src_CodeBeaker.Core/    # Core 프로젝트
│   ├── FileQueue.cs.html   # 파일별 커버리지
│   └── FileStorage.cs.html
└── src_CodeBeaker.Runtimes/
    ├── PythonRuntime.cs.html
    └── ...
```

**색상 코드:**
- 🟢 녹색: 커버됨 (실행됨)
- 🔴 빨강: 커버 안 됨 (실행 안 됨)
- 🟡 노랑: 부분 커버 (조건부 분기)

---

## 지속적 개선

### 테스트 품질 지표

**1. 신뢰도 (Reliability)**
- ❌ Flaky 테스트 비율: < 1%
- ✅ 일관된 실행 결과
- ⏱️ 타임아웃 설정 적절

**2. 속도 (Speed)**
- Unit Tests: < 30초
- Integration Tests: < 2분
- 전체 테스트: < 5분

**3. 유지보수성 (Maintainability)**
- ✅ 명확한 테스트 이름
- ✅ Arrange-Act-Assert 패턴
- ✅ 최소한의 설정 코드

### 테스트 추가 가이드

**새 기능 추가 시:**
1. 단위 테스트 먼저 작성 (TDD)
2. 커버리지 85% 이상 유지
3. Integration 테스트 필요 시 추가

**테스트 작성 체크리스트:**
- [ ] Happy path 테스트
- [ ] Error/Exception 케이스
- [ ] Boundary 조건
- [ ] Null/Empty 입력
- [ ] 동시성 시나리오 (필요 시)

### 자동화 개선 로드맵

**Phase 1 (완료):**
- ✅ 로컬 테스트 자동화 스크립트
- ✅ GitHub Actions CI/CD
- ✅ 커버리지 리포트
- ✅ Watch 모드

**Phase 2 (계획):**
- ⏳ Mutation Testing (Stryker.NET)
- ⏳ Performance Regression Tests
- ⏳ 자동 성능 벤치마크 비교
- ⏳ Slack/Discord 알림 통합

**Phase 3 (미래):**
- 📝 Visual Regression Testing
- 📝 E2E 테스트 자동화 (Playwright)
- 📝 Chaos Engineering 테스트
- 📝 프로덕션 스모크 테스트

---

## 자주 묻는 질문 (FAQ)

### Q1: 테스트가 실패하면 어떻게 하나요?

**A:** 다음 단계를 따르세요:
1. 로컬에서 해당 테스트만 실행하여 재현
2. 테스트 로그 확인 (`--logger "console;verbosity=detailed"`)
3. 실패 원인 분석 (코드 변경, 환경 문제 등)
4. 수정 후 재실행
5. 여전히 실패 시 GitHub Issue 생성

### Q2: Docker 이미지 없이 Integration 테스트를 실행할 수 있나요?

**A:** 아니요. Integration 테스트는 Docker 런타임 이미지가 필요합니다.
```powershell
# 이미지 빌드
.\scripts\build-runtime-images.ps1

# 또는 Integration 테스트 스킵
.\scripts\run-all-tests.ps1 -SkipIntegration
```

### Q3: CI/CD에서 테스트가 통과했는데 로컬에서 실패합니다.

**A:** 환경 차이를 확인하세요:
- .NET SDK 버전 (`dotnet --version`)
- Docker 버전 및 실행 상태
- 의존성 버전 (`dotnet restore`)
- 로컬 캐시 정리 (`dotnet clean`)

### Q4: 커버리지를 높이려면?

**A:** 다음 전략을 시도하세요:
1. 커버리지 리포트에서 빨간색 영역 확인
2. 누락된 분기 조건 테스트 추가
3. 예외 처리 경로 테스트
4. Edge case 시나리오 추가

### Q5: Watch 모드가 너무 느려요.

**A:** 다음 최적화를 적용하세요:
```powershell
# 특정 테스트만 감시
.\scripts\test-watch.ps1 -Target Core

# dotnet watch 사용 (더 빠름)
cd tests/CodeBeaker.Core.Tests
dotnet watch test
```

---

## 추가 리소스

### 문서
- [개발자 가이드](../../DEV_GUIDE.md)
- [사용자 가이드](../USAGE.md)
- [아키텍처 문서](./CSHARP_ARCHITECTURE.md)

### 도구
- [xUnit 문서](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [Codecov](https://about.codecov.io/)

### 커뮤니티
- GitHub Issues
- GitHub Discussions
- Stack Overflow (태그: `codebeaker`)

---

**테스트 자동화로 더 빠르고 안정적인 개발을! 🚀**

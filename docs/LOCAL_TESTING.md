# 🧪 로컬 테스트 가이드

CodeBeaker의 로컬 환경에서 전체 파이프라인을 시뮬레이션하고 모니터링하는 방법을 설명합니다.

---

## 📋 목차

1. [빠른 시작](#빠른-시작)
2. [파이프라인 시뮬레이션](#파이프라인-시뮬레이션)
3. [실시간 모니터링](#실시간-모니터링)
4. [테스트 시나리오](#테스트-시나리오)
5. [CI/CD 비교](#cicd-비교)

---

## 빠른 시작

### 1. 전체 파이프라인 시뮬레이션

```powershell
# 10개 테스트 실행 (기본)
.\scripts\simulate-pipeline.ps1

# 5개 테스트만 빠르게
.\scripts\simulate-pipeline.ps1 -TestCount 5

# 빌드 스킵 (이미 빌드된 경우)
.\scripts\simulate-pipeline.ps1 -SkipBuild

# 상세 출력
.\scripts\simulate-pipeline.ps1 -Verbose
```

### 2. 실시간 모니터링

```powershell
# Terminal 1: 파이프라인 실행
.\scripts\simulate-pipeline.ps1 -TestCount 20

# Terminal 2: 모니터링
.\scripts\monitor-pipeline.ps1
```

---

## 파이프라인 시뮬레이션

### `simulate-pipeline.ps1`

실제 프로덕션 환경처럼 API와 Worker를 실행하고 전체 워크플로우를 테스트합니다.

#### 실행 단계

1. **환경 검증**
   - .NET SDK 버전 확인
   - Docker 실행 상태
   - Docker 런타임 이미지 존재 여부

2. **프로젝트 빌드**
   - Release 모드 빌드
   - 의존성 복원

3. **프로세스 정리**
   - 기존 dotnet 프로세스 종료
   - 임시 디렉토리 정리

4. **큐/저장소 초기화**
   - 임시 디렉토리 생성
   - `codebeaker-queue-sim`
   - `codebeaker-storage-sim`

5. **API 서버 시작**
   - 포트: 5050
   - Health Check 확인
   - 최대 10초 대기

6. **Worker 서비스 시작**
   - 큐 폴링 시작
   - Docker 런타임 준비

7. **코드 실행 테스트**
   - 4개 언어 (Python, JS, Go, C#)
   - 다양한 시나리오 (Hello World, Loop, Math 등)
   - 실시간 진행 상황 표시

8. **프로세스 종료**
   - API와 Worker 정리
   - 리소스 해제

9. **결과 요약**
   - 통과/실패 통계
   - 언어별 성공률
   - 실행 시간

#### 출력 예시

```
╔════════════════════════════════════════════════════════════╗
║         CodeBeaker 파이프라인 시뮬레이션                  ║
╚════════════════════════════════════════════════════════════╝

▶ 1. 환경 검증
============================================================
✅ .NET SDK 8.0.306 설치됨
✅ Docker 실행 중
✅ 모든 Docker 런타임 이미지 존재

▶ 2. 프로젝트 빌드
============================================================
✅ 빌드 완료

▶ 5. API 서버 시작
============================================================
ℹ️  API 서버 시작 중... (PID: 12345)
✅ API 서버 준비 완료 (http://localhost:5050)

▶ 6. Worker 서비스 시작
============================================================
ℹ️  Worker 서비스 시작 중... (PID: 12346)
✅ Worker 서비스 시작 완료

▶ 7. 코드 실행 테스트 (10개)
============================================================

[####################] 100% - C# LINQ

╔════════════════════════════════════════════════════════════╗
║                    최종 결과                               ║
╚════════════════════════════════════════════════════════════╝

총 테스트: 10
통과: 10
실패: 0
통과율: 100%
총 소요 시간: 25.5초

언어별 통계:
Language   Passed Total Rate
--------   ------ ----- ----
python     3      3     100%
javascript 3      3     100%
go         2      2     100%
csharp     2      2     100%

🎉 모든 테스트 통과!
```

### 매개변수

| 매개변수 | 타입 | 기본값 | 설명 |
|---------|------|--------|------|
| `-TestCount` | int | 10 | 실행할 테스트 개수 |
| `-Timeout` | int | 30 | 결과 대기 시간 (초) |
| `-SkipBuild` | switch | false | 빌드 단계 스킵 |
| `-Verbose` | switch | false | 상세 출력 |

### 테스트 케이스

**Python (3개):**
- Hello World
- For Loop
- Error Handling

**JavaScript (3개):**
- Console Log
- Array Reduce
- Async Timeout

**Go (2개):**
- Hello World
- Math Operations

**C# (2개):**
- Hello World
- LINQ Sum

---

## 실시간 모니터링

### `monitor-pipeline.ps1`

파이프라인 실행 중 큐와 저장소 상태를 실시간으로 모니터링합니다.

#### 사용법

```powershell
# 기본 실행 (1초 간격)
.\scripts\monitor-pipeline.ps1

# 5초 간격
.\scripts\monitor-pipeline.ps1 -RefreshInterval 5

# 커스텀 경로
.\scripts\monitor-pipeline.ps1 -QueuePath "C:\temp\queue" -StoragePath "C:\temp\storage"
```

#### 화면 구성

```
╔════════════════════════════════════════════════════════════╗
║         CodeBeaker 파이프라인 모니터                      ║
╚════════════════════════════════════════════════════════════╝

Queue Path: C:\Users\...\codebeaker-queue-sim
Storage Path: C:\Users\...\codebeaker-storage-sim
Press Ctrl+C to exit

============================================================
 실행 시간: 00:02:15
============================================================

📦 작업 큐
  대기 중   : [--------------------] 0/10
  처리 중   : [##------------------] 2/10
  완료      : [################----] 8/10

💾 실행 결과
  완료      : [################----] 8/10
  실행 중   : [##------------------] 2/10
  실패      : [--------------------] 0/10

📊 통계
  총 작업   : 10
  성공률    : 80%
```

### 기능

- **실시간 업데이트**: 지정된 간격으로 자동 갱신
- **진행률 바**: 시각적 진행 상황
- **색상 코드**:
  - 🟡 노랑: 대기 중
  - 🔵 파랑: 처리 중
  - 🟢 초록: 완료
  - 🔴 빨강: 실패

---

## 테스트 시나리오

### 시나리오 1: 빠른 검증

```powershell
# 5개 테스트만 빠르게 실행
.\scripts\simulate-pipeline.ps1 -TestCount 5 -SkipBuild
```

**용도**: PR 생성 전 빠른 확인

### 시나리오 2: 전체 검증

```powershell
# 모든 테스트 케이스 실행
.\scripts\simulate-pipeline.ps1 -TestCount 10 -Verbose
```

**용도**: Release 전 완전한 검증

### 시나리오 3: 부하 테스트

```powershell
# Terminal 1: 많은 테스트 실행
.\scripts\simulate-pipeline.ps1 -TestCount 50

# Terminal 2: 모니터링
.\scripts\monitor-pipeline.ps1 -RefreshInterval 1
```

**용도**: 동시성 및 성능 테스트

### 시나리오 4: 실패 케이스 테스트

```powershell
# 타임아웃 짧게 설정하여 실패 유도
.\scripts\simulate-pipeline.ps1 -Timeout 5 -Verbose
```

**용도**: 에러 처리 검증

---

## CI/CD 비교

### 로컬 시뮬레이션 vs CI/CD

| 항목 | 로컬 시뮬레이션 | CI/CD (GitHub Actions) |
|------|----------------|------------------------|
| **실행 환경** | 개발자 PC | GitHub 서버 |
| **시작 시간** | 즉시 | Push/PR 시 |
| **테스트 범위** | End-to-End (API+Worker) | 유닛 테스트만 |
| **실행 시간** | ~30초 (10개 테스트) | ~3분 (36개 유닛 테스트) |
| **Docker 필요** | ✅ | ❌ |
| **실시간 모니터링** | ✅ | ❌ |
| **비용** | 무료 (로컬 리소스) | 무료 (GitHub Free) |

### CI/CD 워크플로우

**간단한 CI** (`.github/workflows/ci-simple.yml`):
```yaml
jobs:
  unit-tests:
    - Core 단위 테스트
    - Runtime 단위 테스트

  code-quality:
    - 코드 포맷팅 검사
```

**특징:**
- ⚡ 빠른 실행 (~3분)
- 🔋 낮은 리소스 사용
- ✅ 유닛 테스트만 (Docker 불필요)
- 📊 Pull Request 자동 검증

---

## 트러블슈팅

### 문제 1: API 서버 시작 실패

**증상:**
```
❌ API 서버 시작 실패
```

**해결:**
1. 포트 5050이 사용 중인지 확인
2. 기존 dotnet 프로세스 종료
3. 빌드 다시 실행

```powershell
Get-Process dotnet | Stop-Process -Force
.\scripts\simulate-pipeline.ps1
```

### 문제 2: Docker 이미지 누락

**증상:**
```
❌ Docker 이미지 누락: codebeaker-python, codebeaker-nodejs
```

**해결:**
```powershell
.\scripts\build-runtime-images.ps1
```

### 문제 3: 테스트 타임아웃

**증상:**
```
❌ Python Loop - Timeout
```

**해결:**
1. Worker가 실행 중인지 확인
2. Docker 리소스 확인
3. 타임아웃 증가

```powershell
.\scripts\simulate-pipeline.ps1 -Timeout 60
```

### 문제 4: 모니터 화면 깨짐

**증상:**
모니터 출력이 제대로 표시되지 않음

**해결:**
1. PowerShell 창 크기 확대
2. 리프레시 간격 증가

```powershell
.\scripts\monitor-pipeline.ps1 -RefreshInterval 3
```

---

## 고급 사용법

### 커스텀 테스트 케이스 추가

`simulate-pipeline.ps1` 파일에서 `$testCases` 배열에 추가:

```powershell
$testCases += @{
    Name = "Custom Python Test"
    Language = "python"
    Code = @"
# Your test code
print('Custom test')
"@
    ExpectedOutput = "Custom test"
}
```

### 결과 로그 저장

```powershell
.\scripts\simulate-pipeline.ps1 | Tee-Object -FilePath "test-results.log"
```

### 스크립트 자동화

```powershell
# 매시간 자동 실행
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 1)
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-File D:\code-beaker\scripts\simulate-pipeline.ps1"
Register-ScheduledTask -TaskName "CodeBeaker Pipeline Test" -Trigger $trigger -Action $action
```

---

## 모범 사례

### 1. 개발 전 검증

```powershell
# 코드 변경 전 baseline 확인
.\scripts\simulate-pipeline.ps1 -TestCount 10
```

### 2. PR 전 완전 검증

```powershell
# 모든 테스트 + 상세 출력
.\scripts\simulate-pipeline.ps1 -TestCount 10 -Verbose > pr-validation.log
```

### 3. 정기적인 부하 테스트

```powershell
# 주말에 장시간 부하 테스트
.\scripts\simulate-pipeline.ps1 -TestCount 100
```

### 4. 실시간 모니터링 습관화

```powershell
# 항상 모니터 창 열어두기
.\scripts\monitor-pipeline.ps1
```

---

## 요약

- 🚀 **빠른 피드백**: 30초 안에 전체 파이프라인 검증
- 👀 **실시간 모니터링**: 진행 상황 시각화
- 🔬 **실제 환경**: API + Worker + Docker 통합 테스트
- 📊 **상세 리포트**: 언어별 통계 및 실패 원인 분석

**로컬 시뮬레이션으로 CI/CD 전에 모든 문제를 발견하세요!** 🎯

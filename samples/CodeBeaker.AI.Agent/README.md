# CodeBeaker AI Agent Sample

> **Phase 12 & 13: AI Agent Integration + Debug & Improvement**
>
> AI 에이전트가 CodeBeaker를 활용하여 자동으로 코드를 작성하고, 디버그하고, 개선하는 샘플

## 🎯 목적

이 샘플은 AI 에이전트(OpenAI)가 CodeBeaker를 활용하는 방법을 보여줍니다:

### Phase 12: 기본 코딩 워크플로우
1. **AI 코드 생성**: OpenAI API를 사용하여 요구사항에 맞는 코드 생성
2. **CodeBeaker 실행**: 생성된 코드를 CodeBeaker에서 안전하게 실행
3. **결과 검증**: 실행 결과를 확인하고 성공/실패 판단

### Phase 13: 디버그 & 개선 워크플로우
4. **버그 탐지 및 수정**: 의도적 버그 코드 실행 → 에러 분석 → 자동 수정 → 재검증
5. **Test-Driven Development**: 테스트 생성 → 구현 → 반복적 개선 → 성공
6. **Unified Diff**: 패치 생성 및 적용으로 변경 사항 추적

## 📋 요구사항

### 필수
- .NET 8.0 SDK
- CodeBeaker API 실행 중 (`dotnet run --project src/CodeBeaker.API`)
- OpenAI API 키 (.env 파일)

### 선택 (런타임)
- Python 3.9+ (Python 코드 실행 시)
- Node.js 18+ (JavaScript 코드 실행 시)

## 🚀 시작하기

### 1. .env 파일 설정

프로젝트 루트(code-beaker/)에 `.env` 파일이 있어야 합니다:

```env
OPENAI_API_KEY=your-api-key-here
OPENAI_MODEL=gpt-4
```

### 2. CodeBeaker API 실행

터미널 1에서 CodeBeaker API 서버 실행:

```bash
cd /path/to/code-beaker
dotnet run --project src/CodeBeaker.API
```

### 3. AI Agent 실행

터미널 2에서 AI Agent 실행:

```bash
cd samples/CodeBeaker.AI.Agent
dotnet run
```

## 📖 사용 방법

### 기본 실행 (데모 시나리오)

```bash
dotnet run
```

3가지 데모 시나리오가 순차적으로 실행됩니다:
1. Factorial 함수
2. 통계 계산 함수
3. 문자열 역순 함수

### 커스텀 태스크 실행

#### Phase 12: Simple Coding
```bash
# Simple 시나리오
dotnet run simple "Write a Python function to check if a number is prime"
```

#### Phase 13: Debug & Fix
```bash
# Debug 시나리오 - 버그 코드 자동 수정
dotnet run debug BugSamples/off_by_one.py
dotnet run debug BugSamples/logic_error.py
dotnet run debug BugSamples/type_error.py
dotnet run debug BugSamples/null_error.py
```

#### Phase 13: Test-Driven Development
```bash
# TDD 시나리오 - 테스트 우선 개발
dotnet run tdd "Write a function to check if a string is a palindrome"
dotnet run tdd "Write a function to calculate GCD of two numbers"
dotnet run tdd "Write a function to find the longest common substring"
```

## 🎬 실행 예시

```
╔═══════════════════════════════════════════════════════════╗
║         CodeBeaker AI Agent - Demo Sample                ║
║         Phase 12: AI Agent Integration                   ║
╚═══════════════════════════════════════════════════════════╝

✅ Loaded .env from: D:\code-beaker\.env

🤖 Using OpenAI Model: gpt-4
🔗 CodeBeaker API: ws://localhost:5039/ws/jsonrpc

Connecting to CodeBeaker...
[CodeBeaker] Connected to CodeBeaker API

[Scenario] Simple Coding: Write a Python function to calculate factorial...
======================================================================

[Step 1] Creating CodeBeaker session...
✅ Session created: session-abc123

[Step 2] Requesting code from OpenAI...
✅ Code generated (245 characters)

--- Generated Code ---
def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n - 1)

print(factorial(5))
print(factorial(10))
--- End Code ---

[Step 3] Writing code to CodeBeaker workspace...
✅ File written: solution.py

[Step 4] Executing code...
✅ Execution successful!

--- Output ---
120
3628800
--- End Output ---

[Step 5] Closing session...
✅ Session closed

======================================================================
✅ Scenario completed successfully!
```

## 🏗️ 아키텍처

```
AI Agent
├── Program.cs                      # 메인 진입점 (Phase 12 & 13)
├── Services/
│   ├── OpenAIService.cs           # OpenAI API 래퍼 (확장됨)
│   └── CodeBeakerClient.cs        # WebSocket JSON-RPC 클라이언트
├── Scenarios/
│   ├── SimpleCodingScenario.cs    # 간단한 코딩 시나리오 (Phase 12)
│   ├── DebugFixScenario.cs        # 버그 탐지 및 수정 (Phase 13)
│   └── TestDrivenScenario.cs      # TDD 워크플로우 (Phase 13)
├── BugSamples/
│   ├── off_by_one.py              # Off-by-one 에러 샘플
│   ├── logic_error.py             # Logic 에러 샘플
│   ├── type_error.py              # Type 에러 샘플
│   └── null_error.py              # Null/None 에러 샘플
└── Models/
    └── JsonRpcMessage.cs        # JSON-RPC 메시지 모델
```

### 워크플로우

```
1. User Task
   ↓
2. AI Agent (OpenAI)
   → Generate Code
   ↓
3. CodeBeaker Client
   → Create Session
   → Write File
   → Execute Code
   → Get Result
   ↓
4. Result Analysis
   → Success: Return Output
   → Failure: Retry with Fix (향후)
```

## 📊 구현된 시나리오

### ✅ Simple Coding

**목적**: AI가 요구사항에 맞는 코드를 생성하고 실행

**단계**:
1. 세션 생성 (Python/JavaScript)
2. AI에게 코드 생성 요청
3. 생성된 코드를 파일에 작성
4. 코드 실행
5. 결과 확인

**예제**:
```bash
dotnet run simple "Write a function to calculate prime numbers up to n"
```

## 🔮 향후 구현 예정

### ⏳ Test-Driven Development (TDD)

**목적**: AI가 테스트를 먼저 작성하고, 테스트를 통과하는 코드 구현

**단계**:
1. AI에게 테스트 케이스 생성 요청
2. 테스트 파일 작성
3. 테스트 실행 (실패 예상)
4. AI에게 구현 요청
5. 구현 코드 작성
6. 테스트 재실행
7. 실패 시 개선 루프
8. 모든 테스트 통과 시 완료

### ⏳ Debug & Fix

**목적**: 버그가 있는 코드를 AI가 분석하고 수정

**단계**:
1. 버그가 있는 코드 실행
2. 에러 메시지 수집
3. AI에게 에러 분석 및 수정 요청
4. 수정된 코드 적용
5. 재실행 및 검증

### ⏳ Multi-File Project

**목적**: 여러 파일로 구성된 프로젝트 생성

**단계**:
1. 프로젝트 구조 생성
2. 각 파일별로 코드 생성
3. 의존성 관리
4. 통합 테스트

## 🧪 테스트

### 수동 테스트

```bash
# 1. CodeBeaker API 실행
dotnet run --project ../../src/CodeBeaker.API

# 2. 다른 터미널에서 AI Agent 실행
dotnet run

# 3. 결과 확인
# - AI가 코드를 생성하는지
# - CodeBeaker에서 코드가 실행되는지
# - 결과가 올바른지
```

### 예상 결과

- ✅ AI가 실행 가능한 코드 생성
- ✅ CodeBeaker 세션 정상 생성
- ✅ 코드 파일 작성 성공
- ✅ 코드 실행 성공
- ✅ 출력 결과 표시

## 🔧 문제 해결

### CodeBeaker API 연결 실패

```
Error: Unable to connect to CodeBeaker
```

**해결**:
1. CodeBeaker API가 실행 중인지 확인
2. WebSocket URL 확인 (기본: ws://localhost:5039/ws/jsonrpc)
3. 방화벽 설정 확인

### OpenAI API 에러

```
Error: OpenAI API Error
```

**해결**:
1. .env 파일에 올바른 API 키가 있는지 확인
2. API 키 유효성 확인 (OpenAI 대시보드)
3. 모델 이름 확인 (gpt-4, gpt-3.5-turbo 등)

### 코드 실행 실패

```
Error: Execution failed
```

**해결**:
1. 올바른 런타임 설치 확인 (Python/Node.js)
2. 생성된 코드 확인 (syntax error 가능성)
3. CodeBeaker 로그 확인

## 📚 참고 자료

- [CodeBeaker API 문서](../../docs-site/docs/api/overview.md)
- [JSON-RPC 2.0 스펙](https://www.jsonrpc.org/specification)
- [OpenAI API 문서](https://platform.openai.com/docs)

## 🤝 기여

이 샘플을 개선하려면:

1. 새로운 시나리오 추가 (`Scenarios/` 디렉토리)
2. 에러 처리 개선
3. 로깅 강화
4. 테스트 자동화

## 📝 라이선스

MIT License - CodeBeaker 프로젝트와 동일

# CodeBeaker Multi-Runtime Architecture 연구

**개발환경(Runtime) 단위 지원 확장 계획 및 설계**

---

## 🎯 개요

현재 CodeBeaker는 Docker 컨테이너 기반으로 모든 언어를 실행합니다. 하지만 **개발환경(Runtime) 단위**로 구분하여, 각 환경의 특성에 맞는 가장 효율적인 격리 방식을 선택할 수 있습니다.

### 핵심 목표
1. **개발환경 단위 지원**: Python, Node.js, Deno, Bun, .NET, JVM, Go, Rust, Ruby 등
2. **언어별 최적 런타임**: 각 개발환경의 특성에 맞는 가장 효율적인 격리 방식
3. **성능 최적화**: 시작 시간, 메모리 사용량, 실행 속도 개선
4. **유연한 아키텍처**: 런타임 추가/변경이 용이한 플러그인 구조
5. **일관된 인터페이스**: 런타임과 무관하게 동일한 API 제공

---

## 🏗️ 개발환경(Runtime) 단위 분류

CodeBeaker는 **언어(Language)**가 아닌 **개발환경(Runtime)**을 기준으로 지원합니다:

```
개발환경 단위
├── Python Runtime      → CPython, PyPy
├── Node.js Runtime     → JavaScript, TypeScript (via ts-node)
├── Deno Runtime        → JavaScript, TypeScript (native)
├── Bun Runtime         → JavaScript, TypeScript (native)
├── .NET Runtime        → C#, F#, VB.NET
├── JVM Runtime         → Java, Kotlin, Scala, Groovy
├── Go Runtime          → Go (native compiler)
├── Rust Runtime        → Rust (native compiler)
└── Ruby Runtime        → Ruby, mruby
```

**장점**:
- ✅ 명확한 경계: 각 Runtime은 독립적인 실행 환경
- ✅ 다중 언어 지원: JVM → Java/Kotlin/Scala, .NET → C#/F#
- ✅ 최적화 용이: Runtime별 특성에 맞는 격리 전략
- ✅ 에코시스템 통합: 패키지 관리, 빌드 도구 통합

---

## 📊 개발환경별 런타임 전략

### 🐍 Python Runtime
**현재 구현**: Docker 컨테이너

**대체 전략**:
```yaml
primary: Docker
reason: "복잡한 의존성 (numpy, pandas, tensorflow)"
alternatives:
  - pyenv + process isolation (개발 환경)
  - conda environments (AI/ML 특화)
```

**특징**:
- 무거운 의존성 → Docker가 최적
- pip, conda 패키지 생태계
- 시스템 라이브러리 필요 (libpython)

---

### 🟢 Node.js Runtime
**현재 구현**: Docker 컨테이너

**경량화 전략**:
```yaml
primary: Docker
alternatives:
  - V8 Isolates (극경량, Cloudflare Workers 방식)
  - nvm + process (개발 환경)
reason: "V8 Isolates는 npm 생태계 제한"
```

**성능 비교**:
```
Docker:      2000ms startup, 250MB memory
V8 Isolates: 5ms startup, 3MB memory (단, npm 제한)
```

---

### 🦕 Deno Runtime
**새로운 지원 대상** → **강력 추천**

**장점**:
- ✅ TypeScript 네이티브 지원
- ✅ 권한 기반 샌드박스
- ✅ 단일 바이너리 (~50MB)
- ✅ Web API 호환

**최적 전략**:
```yaml
execution: Native Deno process
isolation: Permission-based (--allow-read, --allow-net)
startup: ~80ms
memory: 30MB
improvement: "Docker 대비 25배 빠름"
```

**실행 예시**:
```bash
# Deno 샌드박스 실행
deno run \
  --allow-read=/workspace \
  --allow-write=/workspace \
  --no-prompt \
  /workspace/script.ts
```

---

### 🥟 Bun Runtime
**새로운 지원 대상** → **고성능 JS/TS**

**장점**:
- ✅ JavaScriptCore 엔진 (빠름)
- ✅ TypeScript/JSX 네이티브
- ✅ npm 완전 호환
- ✅ 내장 번들러, 테스트 러너

**최적 전략**:
```yaml
execution: Native Bun process
isolation: Process-level
startup: ~50ms
memory: 25MB
use_case: "npm 생태계 + 성능 필요 시"
```

---

### ⚙️ .NET Runtime
**현재 구현**: Docker 컨테이너

**경량화 전략**:
```yaml
primary: Docker
alternatives:
  - Native AOT (ahead-of-time compilation)
  - Self-contained deployment
reason: "대형 런타임 (~200MB), 컨테이너가 효율적"
```

**특징**:
- C#, F#, VB.NET 지원
- NuGet 패키지 생태계
- 강력한 타입 시스템

---

### ☕ JVM Runtime
**현재 구현**: Docker 컨테이너

**경량화 전략**:
```yaml
primary: Docker
alternatives:
  - GraalVM Native Image (AOT 컴파일)
  - jlink (custom JRE)
reason: "JVM 워밍업 필요, 컨테이너가 안정적"
```

**지원 언어**:
- Java, Kotlin, Scala, Groovy, Clojure
- Maven/Gradle 빌드 시스템

**GraalVM Native Image** (향후 고려):
```
Startup: 1-10ms (vs JVM 1-3초)
Memory: 10-50MB (vs JVM 100-200MB)
```

---

### 🐹 Go Runtime
**현재 구현**: Docker 컨테이너

**경량화 전략**:
```yaml
primary: Docker
alternatives:
  - Native binary compilation (최적)
  - WASM via TinyGo (샌드박스 필요시)
reason: "Docker는 격리, Native는 성능"
```

**특징**:
- 단일 바이너리 배포
- 빠른 컴파일 속도
- 경량 컨테이너 이미지 가능 (~10MB)

---

### 🦀 Rust Runtime
**현재 구현**: Docker 컨테이너

**경량화 전략** → **WASM 강력 추천**:
```yaml
primary: WebAssembly (Wasmer)
alternatives:
  - Docker (시스템 API 필요시)
  - Native binary (격리 불필요시)
reason: "WASM 샌드박스 + 거의 네이티브 성능"
```

**WASM 성능**:
```
Docker Rust: 1500ms startup, 150MB memory
Wasmer WASM: 8ms startup, 12MB memory
Improvement: 187배 빠름, 12배 적은 메모리
```

**실행 예시**:
```bash
# Rust → WASM 컴파일
cargo build --target wasm32-wasi

# Wasmer 실행
wasmer run program.wasm
```

---

### 💎 Ruby Runtime
**새로운 지원 대상**

**전략**:
```yaml
primary: Docker
alternatives:
  - rbenv + process isolation
  - mruby (경량 Ruby, 임베디드)
reason: "의존성 관리 + 격리"
```

---

## 📊 런타임 비교 분석 (기존 내용)

### 1. Docker 컨테이너
**현재 구현**: 모든 언어

**장점**:
- ✅ 강력한 격리 (커널 네임스페이스)
- ✅ 표준화된 이미지 관리
- ✅ 풍부한 에코시스템
- ✅ 네트워크/파일시스템 격리

**단점**:
- ❌ 무거운 오버헤드 (100-300MB+ 이미지)
- ❌ 시작 시간 느림 (1-3초)
- ❌ 리소스 사용량 높음

**적합한 언어**:
- Python (복잡한 의존성)
- Go (컴파일 필요)
- C#/.NET (대형 런타임)
- 시스템 레벨 작업 필요한 경우

---

### 2. Deno Runtime
**적용 가능 언어**: JavaScript, TypeScript

**장점**:
- ✅ 초경량 (단일 바이너리, ~50MB)
- ✅ 빠른 시작 (< 100ms)
- ✅ 내장 권한 시스템 (--allow-net, --allow-read 등)
- ✅ 샌드박스 기본 제공
- ✅ TypeScript 네이티브 지원
- ✅ Web API 호환성

**단점**:
- ❌ Node.js 생태계와 부분 호환
- ❌ npm 패키지 지원 제한적

**보안 모델**:
```typescript
// Deno 권한 기반 실행
deno run \
  --allow-read=/workspace \
  --allow-write=/workspace \
  --no-prompt \
  /workspace/script.ts
```

**성능 벤치마크** (예상):
```
Docker Node.js:
- 시작 시간: ~2초
- 메모리: 200-300MB

Deno:
- 시작 시간: ~50-100ms
- 메모리: 20-50MB

→ 20-40배 빠른 시작, 5-10배 적은 메모리
```

---

### 3. Wasmer/WebAssembly
**적용 가능 언어**: Rust, C/C++, AssemblyScript, Go (TinyGo)

**장점**:
- ✅ 초고속 시작 (< 10ms)
- ✅ 초경량 (수 MB)
- ✅ 샌드박스 보안 (WASI)
- ✅ 크로스 플랫폼 바이너리
- ✅ 거의 네이티브 성능

**단점**:
- ❌ 제한된 시스템 API (WASI)
- ❌ 아직 성숙하지 않은 생태계
- ❌ 파일 I/O, 네트워크 제한

**사용 사례**:
```rust
// Rust → WASM 컴파일
rustc --target wasm32-wasi main.rs

// Wasmer로 실행
wasmer run main.wasm \
  --dir=/workspace \
  --mapdir /workspace:/host/workspace
```

**성능 벤치마크** (예상):
```
Docker Rust:
- 시작 시간: ~1-2초
- 메모리: 100-200MB

WASM (Wasmer):
- 시작 시간: ~5-10ms
- 메모리: 5-20MB

→ 100-200배 빠른 시작, 10-20배 적은 메모리
```

---

### 4. V8 Isolates (Node.js Workers)
**적용 가능 언어**: JavaScript

**장점**:
- ✅ 극도로 빠른 시작 (< 5ms)
- ✅ 초경량 (수 KB)
- ✅ 메모리 격리
- ✅ 동일 프로세스 내 실행

**단점**:
- ❌ 약한 보안 격리 (동일 프로세스)
- ❌ CPU/메모리 제한 어려움
- ❌ 파일시스템 접근 제한

**사용 사례**:
```javascript
// V8 Isolates (Cloudflare Workers 스타일)
const worker = new Worker('./script.js', {
  eval: false,
  resourceLimits: {
    maxOldGenerationSizeMb: 128,
    maxYoungGenerationSizeMb: 64
  }
});
```

**적합한 시나리오**:
- 단순 계산 작업
- 짧은 실행 시간 (< 1초)
- 파일 I/O 불필요

---

### 5. Firecracker MicroVM
**적용 가능 언어**: 모든 언어

**장점**:
- ✅ 빠른 시작 (125ms)
- ✅ 강력한 격리 (KVM 기반)
- ✅ 경량 (커널 + 런타임만)
- ✅ 멀티테넌시 안전

**단점**:
- ❌ 복잡한 설정
- ❌ Linux 전용
- ❌ 오버헤드 여전히 존재

**사용 사례**:
```bash
# Firecracker 시작
firectl \
  --kernel=vmlinux \
  --root-drive=rootfs.ext4 \
  --memory=512 \
  --cpus=1
```

---

### 6. QuickJS (경량 JavaScript 엔진)
**적용 가능 언어**: JavaScript, ES2020

**장점**:
- ✅ 초경량 (600KB 바이너리)
- ✅ 빠른 시작 (< 10ms)
- ✅ 완전한 ES2020 지원
- ✅ C API 제공

**단점**:
- ❌ V8보다 느린 실행 속도
- ❌ npm 생태계 미지원
- ❌ 제한된 Web API

**사용 사례**:
```bash
# QuickJS 실행
qjs --std script.js
```

---

### 7. GraalVM Native Image
**적용 가능 언어**: Java, JavaScript, Python, Ruby, R

**장점**:
- ✅ AOT 컴파일 → 빠른 시작
- ✅ 낮은 메모리 사용
- ✅ 다중 언어 지원
- ✅ 네이티브 바이너리

**단점**:
- ❌ 빌드 시간 김
- ❌ 리플렉션 제한
- ❌ 복잡한 설정

---

## 🏗️ Multi-Runtime 아키텍처 설계

### 아키텍처 개요

```
┌─────────────────────────────────────────┐
│         CodeBeaker API                  │
│         (JSON-RPC 2.0)                  │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│      Runtime Selection Layer            │
│   (언어별 최적 런타임 선택)               │
└──────────────┬──────────────────────────┘
               │
      ┌────────┼────────┬────────┬────────┐
      ▼        ▼        ▼        ▼        ▼
  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐
  │Docker│ │ Deno │ │WASM │ │V8 Iso│ │Native│
  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘
```

### 핵심 인터페이스

```csharp
public interface IExecutionRuntime
{
    string Name { get; }
    RuntimeType Type { get; }

    // 런타임 가용성 확인
    Task<bool> IsAvailableAsync(CancellationToken ct);

    // 격리된 환경 생성
    Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken ct
    );

    // 성능 특성
    RuntimeCapabilities GetCapabilities();
}

public interface IExecutionEnvironment : IAsyncDisposable
{
    string EnvironmentId { get; }
    RuntimeType RuntimeType { get; }

    // 명령 실행
    Task<ExecutionResult> ExecuteAsync(
        Command command,
        CancellationToken ct
    );

    // 상태 관리
    Task<EnvironmentState> GetStateAsync(CancellationToken ct);
}

public enum RuntimeType
{
    // 개발환경 단위
    Docker,           // 강력한 격리, 모든 개발환경 지원
    PythonRuntime,    // Python (CPython, PyPy)
    NodeJsRuntime,    // Node.js (JS, TS via ts-node)
    DenoRuntime,      // Deno (JS, TS native)
    BunRuntime,       // Bun (JS, TS native)
    DotNetRuntime,    // .NET (C#, F#, VB)
    JvmRuntime,       // JVM (Java, Kotlin, Scala)
    GoRuntime,        // Go (native compiler)
    RustRuntime,      // Rust (native or WASM)
    RubyRuntime,      // Ruby (mruby)

    // 특수 실행 방식
    Wasmer,           // WebAssembly runtime (WASM)
    V8Isolate,        // V8 Isolates (극경량 JS)
    Firecracker,      // MicroVM (모든 언어)
    GraalVMNative,    // GraalVM Native Image (AOT)
    NativeProcess     // 네이티브 프로세스 (개발용)
}
```

---

## 📋 개발환경별 최적 런타임 매핑

### Decision Matrix

| 개발환경 | 지원 언어 | 1순위 실행 방식 | 2순위 | 선택 기준 |
|----------|-----------|----------------|-------|-----------|
| **Python Runtime** | Python | Docker | Process | 복잡한 의존성 (numpy, pandas) |
| **Node.js Runtime** | JS, TS | Docker | V8 Isolate | npm 생태계 완전 지원 |
| **Deno Runtime** | JS, TS | Native Deno | - | TypeScript 네이티브 + 권한 샌드박스 |
| **Bun Runtime** | JS, TS | Native Bun | - | 고성능 + npm 호환 |
| **.NET Runtime** | C#, F# | Docker | Native AOT | 대형 런타임 (~200MB) |
| **JVM Runtime** | Java, Kotlin, Scala | Docker | GraalVM Native | JVM 워밍업 필요 |
| **Go Runtime** | Go | Docker | Native Binary | 표준 라이브러리 + 격리 |
| **Rust Runtime** | Rust | Wasmer (WASM) | Docker | 샌드박스 + 거의 네이티브 성능 |
| **Ruby Runtime** | Ruby | Docker | Process (rbenv) | Gem 의존성 관리 |

### 구체적인 매핑 전략

#### JavaScript/TypeScript → **Deno** (1순위)

**선택 이유**:
- 내장 권한 시스템으로 안전한 샌드박스
- 빠른 시작 시간 (< 100ms)
- TypeScript 네이티브 지원
- 모듈 시스템 간단

**구현**:
```csharp
public class DenoRuntime : IExecutionRuntime
{
    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken ct)
    {
        var deno = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "deno",
                Arguments = "run --no-prompt --allow-read=/workspace --allow-write=/workspace",
                WorkingDirectory = "/workspace",
                // Deno 권한 설정
                Environment =
                {
                    ["DENO_DIR"] = "/tmp/deno-cache",
                    ["NO_COLOR"] = "1"
                }
            }
        };

        await deno.StartAsync(ct);

        return new DenoEnvironment(deno);
    }
}
```

**Session 예제**:
```json
{
  "jsonrpc": "2.0",
  "method": "session.create",
  "params": {
    "language": "javascript",
    "runtime": "deno",  // 명시적 런타임 선택
    "config": {
      "permissions": {
        "allowRead": ["/workspace"],
        "allowWrite": ["/workspace"],
        "allowNet": false
      }
    }
  }
}
```

---

#### Rust → **Wasmer (WASM)** (1순위)

**선택 이유**:
- 컴파일된 WASM 실행 → 극도로 빠름
- 샌드박스 보안
- 파일 I/O 제한으로 안전

**구현**:
```csharp
public class WasmerRuntime : IExecutionRuntime
{
    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken ct)
    {
        // 1. Rust 코드 → WASM 컴파일
        await CompileToWasmAsync(config.Code, ct);

        // 2. Wasmer로 실행
        var wasmer = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "wasmer",
                Arguments = $"run output.wasm --dir=/workspace --mapdir /workspace:{config.WorkspacePath}",
            }
        };

        await wasmer.StartAsync(ct);

        return new WasmerEnvironment(wasmer);
    }

    private async Task CompileToWasmAsync(string code, CancellationToken ct)
    {
        // rustc --target wasm32-wasi -o output.wasm
        var rustc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "rustc",
                Arguments = "--target wasm32-wasi -o output.wasm input.rs"
            }
        };

        await rustc.RunAsync(ct);
    }
}
```

---

#### Python → **Docker** (유지)

**선택 이유**:
- 복잡한 의존성 (numpy, pandas, scipy 등)
- C 확장 모듈 필요
- 대체 런타임 없음

**최적화 방안**:
```dockerfile
# 경량화된 Python 이미지
FROM python:3.12-alpine

# 필수 패키지만 사전 설치
RUN pip install --no-cache-dir numpy pandas

WORKDIR /workspace
```

---

## 🎯 구현 로드맵

### Phase 1: Runtime Abstraction Layer (2주)

**목표**: 런타임 독립적인 인터페이스 구현

**작업**:
1. `IExecutionRuntime` 인터페이스 설계
2. `IExecutionEnvironment` 인터페이스 설계
3. `RuntimeSelector` 구현 (언어별 런타임 선택 로직)
4. 기존 `DockerRuntime`을 새 인터페이스로 리팩토링

**구현**:
```csharp
public class RuntimeSelector
{
    private readonly Dictionary<string, List<IExecutionRuntime>> _runtimeMapping;

    public IExecutionRuntime SelectBestRuntime(
        string language,
        RuntimePreference preference)
    {
        var availableRuntimes = _runtimeMapping[language];

        return preference switch
        {
            RuntimePreference.Speed => availableRuntimes
                .OrderBy(r => r.GetCapabilities().StartupTime)
                .First(),

            RuntimePreference.Security => availableRuntimes
                .OrderByDescending(r => r.GetCapabilities().IsolationLevel)
                .First(),

            RuntimePreference.Memory => availableRuntimes
                .OrderBy(r => r.GetCapabilities().MemoryOverhead)
                .First(),

            _ => availableRuntimes.First()
        };
    }
}
```

---

### Phase 2: Deno Runtime 구현 (1-2주)

**목표**: JavaScript/TypeScript용 Deno 런타임 추가

**작업**:
1. `DenoRuntime` 클래스 구현
2. Deno 권한 시스템 통합
3. 성능 벤치마크
4. 통합 테스트

**성능 목표**:
- 시작 시간: < 100ms
- 메모리 사용: < 50MB
- Docker 대비 20배 빠른 시작

---

### Phase 3: Wasmer Runtime 구현 (2-3주)

**목표**: Rust용 WASM 런타임 추가

**작업**:
1. `WasmerRuntime` 클래스 구현
2. Rust → WASM 컴파일 파이프라인
3. WASI 파일시스템 매핑
4. 성능 벤치마크

**성능 목표**:
- 시작 시간: < 10ms
- 메모리 사용: < 20MB
- Docker 대비 100배 빠른 시작

---

### Phase 4: Runtime Selection UI (1주)

**목표**: 사용자가 런타임 선택 가능

**JSON-RPC API**:
```json
{
  "jsonrpc": "2.0",
  "method": "session.create",
  "params": {
    "language": "javascript",
    "runtime": "auto",  // "auto", "docker", "deno", "wasmer"
    "runtimePreference": "speed"  // "speed", "security", "memory"
  }
}
```

---

## 📊 예상 성능 개선

### JavaScript 실행

| 메트릭 | Docker | Deno | 개선 |
|--------|--------|------|------|
| 시작 시간 | 2000ms | 80ms | **25배** |
| 메모리 | 250MB | 30MB | **8배** |
| Hello World | 2.5초 | 0.1초 | **25배** |

### Rust 실행

| 메트릭 | Docker | Wasmer | 개선 |
|--------|--------|--------|------|
| 시작 시간 | 1500ms | 8ms | **187배** |
| 메모리 | 150MB | 12MB | **12배** |
| 피보나치 | 2.0초 | 0.01초 | **200배** |

---

## 🔒 보안 고려사항

### 런타임별 보안 모델

#### Docker
- ✅ 커널 네임스페이스 격리
- ✅ Cgroups 리소스 제한
- ✅ Seccomp 시스템콜 필터링
- **보안 등급**: ⭐⭐⭐⭐⭐

#### Deno
- ✅ 권한 기반 시스템
- ✅ 샌드박스 기본 제공
- ⚠️ 동일 호스트 실행
- **보안 등급**: ⭐⭐⭐⭐

#### Wasmer (WASM)
- ✅ WASI 샌드박스
- ✅ 메모리 격리
- ⚠️ 제한된 시스템 API
- **보안 등급**: ⭐⭐⭐⭐

#### V8 Isolates
- ⚠️ 약한 격리
- ⚠️ 동일 프로세스
- ❌ 리소스 제한 어려움
- **보안 등급**: ⭐⭐

### 보안 정책

```yaml
security_policies:
  production:
    javascript: "deno"      # 충분한 격리
    python: "docker"        # 강력한 격리 필요
    rust: "wasmer"         # WASM 샌드박스

  development:
    javascript: "v8isolate" # 빠른 반복
    python: "docker"
    rust: "native"         # 디버깅 용이

  trusted_code:
    javascript: "native"   # 최대 성능
    rust: "native"
```

---

## 💡 사용 시나리오

### 시나리오 1: AI 코드 생성 플랫폼

**요구사항**:
- 빠른 피드백 (< 500ms)
- 높은 동시성 (1000+ 요청/분)
- 간단한 코드 스니펫

**최적 구성**:
```yaml
languages:
  javascript: "deno"      # 빠른 시작
  typescript: "deno"      # 네이티브 지원
  python: "docker"        # 의존성 필요
  rust: "wasmer"         # 초고속
```

**예상 성능**:
- JavaScript: 100ms (Docker 대비 20배 빠름)
- Rust: 20ms (Docker 대비 100배 빠름)

---

### 시나리오 2: 교육 플랫폼

**요구사항**:
- 학생 코드 안전 실행
- 리소스 제한
- 적당한 성능

**최적 구성**:
```yaml
languages:
  javascript: "deno"      # 권한 시스템
  python: "docker"        # 격리 중요
  java: "graalvm"        # AOT 컴파일
```

---

### 시나리오 3: Serverless Functions

**요구사항**:
- 콜드 스타트 최소화
- 높은 처리량
- 낮은 메모리

**최적 구성**:
```yaml
languages:
  javascript: "v8isolate" # 극도로 빠름
  rust: "wasmer"         # WASM 최적화
```

---

## 🚀 마이그레이션 전략

### 단계적 도입

#### Step 1: Docker 유지 (현재)
- 모든 언어 Docker로 실행
- 안정성 확보

#### Step 2: Deno 추가 (1개월)
- JavaScript/TypeScript만 Deno 옵션 제공
- Docker와 병행 운영
- 사용자 선택 가능

#### Step 3: 성능 검증 (2주)
- 벤치마크 실행
- 안정성 테스트
- 사용자 피드백

#### Step 4: Wasmer 추가 (1.5개월)
- Rust 지원
- WASM 컴파일 파이프라인
- 성능 최적화

#### Step 5: 기본값 변경 (선택)
- Deno를 JavaScript 기본값으로
- Docker는 폴백으로 유지

---

## 📚 기술 스택 요구사항

### 필수 설치

```bash
# Deno
curl -fsSL https://deno.land/install.sh | sh

# Wasmer
curl https://get.wasmer.io -sSfL | sh

# Rust (WASM 타겟)
rustup target add wasm32-wasi

# QuickJS (선택)
git clone https://github.com/bellard/quickjs.git
cd quickjs && make && sudo make install
```

### 런타임 버전 관리

```csharp
public class RuntimeVersionManager
{
    public async Task<RuntimeInfo> GetRuntimeInfoAsync(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.Deno => await GetDenoVersionAsync(),
            RuntimeType.Wasmer => await GetWasmerVersionAsync(),
            _ => throw new NotSupportedException()
        };
    }

    private async Task<RuntimeInfo> GetDenoVersionAsync()
    {
        var version = await ExecuteAsync("deno --version");
        return new RuntimeInfo
        {
            Type = RuntimeType.Deno,
            Version = ParseVersion(version),
            Available = !string.IsNullOrEmpty(version)
        };
    }
}
```

---

## 🎯 성공 지표

### 성능 목표

| 메트릭 | 현재 (Docker) | 목표 (Multi-Runtime) | 개선 |
|--------|---------------|---------------------|------|
| JS 시작 시간 | 2000ms | 100ms | 20배 |
| Rust 시작 시간 | 1500ms | 10ms | 150배 |
| 메모리 (JS) | 250MB | 30MB | 8배 |
| 메모리 (Rust) | 150MB | 15MB | 10배 |
| 동시 세션 | 100 | 500+ | 5배 |

### 품질 목표

- ✅ 보안: 모든 런타임 샌드박스 제공
- ✅ 안정성: 99.9% 가용성
- ✅ 호환성: 기존 API 100% 호환
- ✅ 확장성: 새 런타임 추가 < 1주

---

## 📖 참고 자료

### Deno
- 공식 문서: https://deno.land/
- 권한 시스템: https://deno.land/manual/basics/permissions
- Deploy: https://deno.com/deploy

### Wasmer
- 공식 문서: https://wasmer.io/
- WASI: https://wasi.dev/
- Wasmer Runtime: https://docs.wasmer.io/

### V8 Isolates
- Cloudflare Workers: https://workers.cloudflare.com/
- V8 Isolates 아키텍처: https://v8.dev/docs

### Firecracker
- AWS Firecracker: https://firecracker-microvm.github.io/
- 아키텍처: https://github.com/firecracker-microvm/firecracker/blob/main/docs/design.md

---

**문서 버전**: 1.0
**작성일**: 2025-10-27
**상태**: 연구 단계 (Phase 1 준비 중)

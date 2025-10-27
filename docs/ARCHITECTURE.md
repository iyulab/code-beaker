# CodeBeaker 아키텍처 설계 문서

## 개요

CodeBeaker는 **Multi-Runtime 지원 세션 기반 코드 실행 플랫폼**으로, WebSocket + JSON-RPC 2.0 프로토콜을 사용하여 실시간 양방향 통신을 제공합니다.

### 핵심 아키텍처 원칙

1. **Multi-Runtime Architecture**: 개발환경별 최적 런타임 자동 선택 (Docker, Deno, Bun 등)
2. **Stateful Execution**: 환경 재사용으로 50-75% 성능 향상
3. **Command Pattern**: Type-safe 명령 시스템
4. **Runtime Abstraction**: IExecutionRuntime 인터페이스 기반 확장 가능 설계
5. **JSON-RPC 2.0**: 표준 프로토콜 준수

---

## 시스템 아키텍처

### 전체 구조

```
┌─────────────────────────────────────────────────────────┐
│                    WebSocket Client                      │
│              (Browser / Node.js / Python)                │
└───────────────────────┬─────────────────────────────────┘
                        │
                        │ JSON-RPC 2.0 over WebSocket
                        ▼
┌─────────────────────────────────────────────────────────┐
│                  CodeBeaker API Server                   │
│                 (ASP.NET Core 8 + Kestrel)               │
│                                                           │
│  ┌────────────────────────────────────────────────────┐  │
│  │            WebSocket Handler                       │  │
│  │  - Connection management                           │  │
│  │  - Message framing                                 │  │
│  │  - Keep-alive (2min interval)                      │  │
│  └────────────────────┬───────────────────────────────┘  │
│                       │                                   │
│                       ▼                                   │
│  ┌────────────────────────────────────────────────────┐  │
│  │            JSON-RPC Router                         │  │
│  │  - Method dispatch (reflection-based)              │  │
│  │  - Request validation                              │  │
│  │  - Error handling                                  │  │
│  │  - Notification support                            │  │
│  └────────────────────┬───────────────────────────────┘  │
│                       │                                   │
│            ┌──────────┴──────────┐                        │
│            │                     │                        │
│            ▼                     ▼                        │
│  ┌──────────────────┐  ┌──────────────────┐              │
│  │ Session Handlers │  │ Legacy Handlers  │              │
│  │ - create         │  │ - execution.run  │              │
│  │ - execute        │  │ - language.list  │              │
│  │ - list           │  │ - initialize     │              │
│  │ - close          │  │                  │              │
│  └────────┬─────────┘  └──────────────────┘              │
│           │                                               │
│           ▼                                               │
│  ┌────────────────────────────────────────────────────┐  │
│  │              Session Manager                       │  │
│  │  - ConcurrentDictionary<string, Session>           │  │
│  │  - SemaphoreSlim for concurrency control           │  │
│  │  - Session lifecycle management                    │  │
│  │  - Auto cleanup (IdleTimeout: 30min)               │  │
│  └────────────────────┬───────────────────────────────┘  │
│                       │                                   │
│                       ▼                                   │
│  ┌────────────────────────────────────────────────────┐  │
│  │              Command Executor                      │  │
│  │  - Pattern matching dispatch                       │  │
│  │  - Docker API direct calls                         │  │
│  │  - 7 command types support                         │  │
│  └────────────────────┬───────────────────────────────┘  │
│                       │                                   │
│  ┌────────────────────┴───────────────────────────────┐  │
│  │          Background Services                       │  │
│  │  - SessionCleanupWorker (1min interval)            │  │
│  │  - Health check monitoring                         │  │
│  └────────────────────────────────────────────────────┘  │
└───────────────────────┬─────────────────────────────────┘
                        │
                        │ Docker API
                        ▼
┌─────────────────────────────────────────────────────────┐
│                    Docker Engine                         │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Session Containers (Long-running)               │   │
│  │  - sleep infinity (keep alive)                   │   │
│  │  - Filesystem persistence                        │   │
│  │  - Resource limits (Memory: 512MB, CPU: 1024)    │   │
│  │  - Network: none (isolated)                      │   │
│  └──────────────────────────────────────────────────┘   │
│                                                           │
│  Runtime Images:                                         │
│  - codebeaker-python:latest  (Python 3.12)              │
│  - codebeaker-nodejs:latest  (Node.js 20)               │
│  - codebeaker-golang:latest  (Go 1.21)                  │
│  - codebeaker-dotnet:latest  (.NET 8)                   │
└─────────────────────────────────────────────────────────┘
```

---

## Multi-Runtime Architecture (Phase 4)

### 개요

Phase 4에서 도입된 Multi-Runtime Architecture는 개발환경별로 최적의 런타임을 자동 선택하여 성능과 격리 수준을 최적화합니다.

### 런타임 타입

```csharp
public enum RuntimeType
{
    Docker,        // 컨테이너 기반 (강력한 격리)
    Deno,          // 프로세스 기반 (빠른 시작)
    Bun,           // 프로세스 기반 (초고속)
    NodeJs,        // 프로세스 기반 (호환성)
    WebAssembly,   // WASM 기반 (극한 격리)
    V8Isolate,     // V8 격리 (경량)
    NativeProcess  // 네이티브 프로세스
}
```

### IExecutionRuntime 인터페이스

**핵심 추상화 계층**:

```csharp
public interface IExecutionRuntime
{
    string Name { get; }
    RuntimeType Type { get; }
    string[] SupportedEnvironments { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken);
    RuntimeCapabilities GetCapabilities();
}

public interface IExecutionEnvironment : IAsyncDisposable
{
    string EnvironmentId { get; }
    RuntimeType RuntimeType { get; }
    EnvironmentState State { get; }

    Task<CommandResult> ExecuteAsync(Command command, CancellationToken ct);
    Task<EnvironmentState> GetStateAsync(CancellationToken ct);
    Task CleanupAsync(CancellationToken ct);
}
```

### RuntimeCapabilities 모델

**성능 특성 정의**:

```csharp
public sealed class RuntimeCapabilities
{
    public int StartupTimeMs { get; set; }       // 시작 시간
    public int MemoryOverheadMB { get; set; }    // 메모리 오버헤드
    public int IsolationLevel { get; set; }      // 격리 수준 (0-10)
    public bool SupportsFilesystemPersistence { get; set; }
    public bool SupportsNetworkAccess { get; set; }
    public int MaxConcurrentExecutions { get; set; }
}
```

### RuntimeSelector 전략

**4가지 선택 전략**:

| 전략 | 기준 | 선택 알고리즘 |
|-----|------|--------------|
| Speed | 시작 속도 우선 | `MIN(StartupTimeMs)` |
| Security | 격리 수준 우선 | `MAX(IsolationLevel)` |
| Memory | 메모리 최소화 | `MIN(MemoryOverheadMB)` |
| Balanced | 균형 점수 | `(속도 + 메모리 + 격리/2)` |

```csharp
var selector = new RuntimeSelector(runtimes);

// 속도 우선 → Deno 선택
var runtime = await selector.SelectBestRuntimeAsync(
    "javascript", RuntimePreference.Speed);

// 보안 우선 → Docker 선택
var runtime = await selector.SelectBestRuntimeAsync(
    "python", RuntimePreference.Security);
```

### Deno Runtime 구현

**JavaScript/TypeScript용 경량 런타임**:

#### 특징
- **시작 시간**: 80ms (Docker 대비 25배 빠름)
- **메모리**: 30MB (Docker 대비 8배 적음)
- **격리**: 권한 기반 샌드박스 (7/10)
- **TypeScript**: 네이티브 지원

#### 권한 시스템
```typescript
// RuntimeConfig에서 권한 설정
{
  "Permissions": {
    "AllowRead": ["/workspace"],
    "AllowWrite": ["/workspace"],
    "AllowNet": false,
    "AllowEnv": false
  }
}

// Deno CLI 인자로 변환
deno run \
  --no-prompt \
  --allow-read=/workspace \
  --allow-write=/workspace \
  script.ts
```

#### 지원 Command 타입
1. **ExecuteCodeCommand**: TypeScript/JavaScript 코드 직접 실행
2. **WriteFileCommand**: 파일 생성/수정
3. **ReadFileCommand**: 파일 읽기
4. **CreateDirectoryCommand**: 디렉토리 생성
5. **ListDirectoryCommand**: 디렉토리 목록

### 성능 비교

#### JavaScript/TypeScript 실행

| 메트릭 | Docker Runtime | Deno Runtime | 개선 |
|--------|---------------|--------------|------|
| 시작 시간 | 2000ms | 80ms | **25배** |
| 메모리 | 250MB | 30MB | **8배** |
| 격리 수준 | 9/10 | 7/10 | -2 |

#### AI 에이전트 시나리오
```
10번 연속 실행:
- Docker: ~3초 (환경 생성 오버헤드)
- Deno: ~1초 (빠른 프로세스 시작)
→ 3배 빠른 응답 속도
```

### 확장 가능성

**향후 추가 예정 런타임**:

1. **Bun Runtime**: JavaScript/TypeScript (Deno보다 빠름)
2. **Wasmer Runtime**: WebAssembly (극한 격리)
3. **V8 Isolate**: JavaScript (클라우드 엣지용)
4. **Native Process**: Go, Rust (컴파일 언어)

---

## 핵심 컴포넌트

### 1. Session Manager

**책임**: Stateful 컨테이너 생명주기 관리

#### 주요 기능
- **세션 생성**: Docker 컨테이너 시작 (`sleep infinity`)
- **세션 풀링**: `ConcurrentDictionary<string, Session>` 기반
- **명령 실행**: 기존 컨테이너 재사용
- **자동 정리**: IdleTimeout (30분), MaxLifetime (120분)

#### 세션 상태 머신
```
Creating → Active → Idle → Closing → Closed
    ↑        ↓       ↓        ↓
    └────────┴───────┴────────┘
       UpdateActivity()
```

#### 코드 구조
```csharp
public sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions;
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly SemaphoreSlim _lock;

    public async Task<Session> CreateSessionAsync(SessionConfig config)
    {
        // 1. Docker 컨테이너 생성 (sleep infinity)
        // 2. 컨테이너 시작
        // 3. Session 객체 생성 및 풀에 추가
        // 4. 세션 ID 반환
    }

    public async Task<CommandResult> ExecuteInSessionAsync(
        string sessionId, Command command)
    {
        // 1. 세션 조회
        // 2. 활동 시각 업데이트
        // 3. CommandExecutor로 명령 실행
        // 4. 세션 상태를 Idle로 전환
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        // 1. 만료된 세션 필터링
        // 2. 각 세션에 대해 CloseSessionAsync() 호출
    }
}
```

---

### 2. Command Executor

**책임**: Docker API 직접 호출로 명령 실행

#### Command 타입 시스템

**7가지 Command 타입** (JSON 다형성):

| Command | 용도 | Docker API |
|---------|------|-----------|
| WriteFileCommand | 파일 작성 | `tee <path>` |
| ReadFileCommand | 파일 읽기 | `cat <path>` |
| ExecuteShellCommand | Shell 실행 | 직접 명령 |
| CreateDirectoryCommand | 디렉토리 생성 | `mkdir -p <path>` |
| CopyFileCommand | 파일 복사 | `cp <src> <dst>` |
| DeleteFileCommand | 파일 삭제 | `rm -rf <path>` |
| ListDirectoryCommand | 목록 조회 | `ls -la <path>` |

#### JSON 다형성 구조
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WriteFileCommand), typeDiscriminator: "write_file")]
[JsonDerivedType(typeof(ReadFileCommand), typeDiscriminator: "read_file")]
// ... 7개 타입
public abstract class Command
{
    public abstract string Type { get; }
    public string? Id { get; set; }
}
```

#### 실행 로직 (Pattern Matching)
```csharp
public async Task<CommandResult> ExecuteAsync(
    Command command, string containerId)
{
    return command switch
    {
        WriteFileCommand write => await ExecuteWriteFileAsync(write, containerId),
        ReadFileCommand read => await ExecuteReadFileAsync(read, containerId),
        ExecuteShellCommand shell => await ExecuteShellAsync(shell, containerId),
        // ... 7가지 패턴
        _ => throw new NotSupportedException($"Command type {command.Type}")
    };
}

private async Task<CommandResult> ExecuteWriteFileAsync(
    WriteFileCommand cmd, string containerId)
{
    // Docker Exec API 사용 (shell 없이!)
    var execConfig = new ContainerExecCreateParameters
    {
        Cmd = new[] { "tee", cmd.Path },
        AttachStdin = true,
        AttachStdout = true
    };

    var execResponse = await _docker.Exec.ExecCreateContainerAsync(
        containerId, execConfig);

    using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(
        execResponse.ID, false);

    var bytes = Encoding.UTF8.GetBytes(cmd.Content);
    await stream.WriteAsync(bytes, 0, bytes.Length);

    return CommandResult.Ok(new { path = cmd.Path, bytes = bytes.Length });
}
```

---

### 3. JSON-RPC Router

**책임**: 메서드 디스패칭 및 에러 처리

#### 메서드 등록 (Reflection-based)
```csharp
public sealed class JsonRpcRouter
{
    private readonly ConcurrentDictionary<string, IJsonRpcHandler> _handlers;

    public void RegisterHandler(IJsonRpcHandler handler)
    {
        _handlers[handler.Method] = handler;
    }

    public async Task<JsonRpcResponse?> ProcessRequestAsync(JsonRpcRequest request)
    {
        // 1. 요청 검증 (jsonrpc: "2.0", method 존재)
        // 2. Notification 처리 (id가 없으면 응답 없음)
        // 3. 메서드 실행
        // 4. 성공/실패 응답 반환
    }
}
```

#### 핸들러 인터페이스
```csharp
public interface IJsonRpcHandler
{
    string Method { get; }
    Task<object?> HandleAsync(object? @params, CancellationToken ct);
}
```

#### 등록된 메서드 (8개)
```
Legacy:
- initialize
- execution.run
- execution.status
- language.list

Session (Phase 3):
- session.create
- session.execute
- session.list
- session.close
```

---

### 4. Runtime System

**책임**: 언어별 실행 환경 제공

#### BaseRuntime (Abstract Class)
```csharp
public abstract class BaseRuntime : IRuntime
{
    protected readonly DockerExecutor _executor;

    public abstract string LanguageName { get; }
    public abstract string DockerImage { get; }
    protected abstract string FileExtension { get; }

    // Legacy (Backward compatibility)
    public abstract string[] GetRunCommand(
        string entryPoint, List<string>? packages = null);

    // Phase 2 (Command-based)
    public abstract List<Command> GetExecutionPlan(
        string code, List<string>? packages = null);

    public async Task<ExecutionResult> ExecuteAsync(
        string code, ExecutionConfig config, CancellationToken ct)
    {
        // 1. Setup workspace (temp directory)
        // 2. Get execution plan (commands)
        // 3. Execute via Docker
        // 4. Cleanup workspace
    }
}
```

#### 언어별 구현 예시 (PythonRuntime)
```csharp
public sealed class PythonRuntime : BaseRuntime
{
    public override string LanguageName => "python";
    public override string DockerImage => "codebeaker-python:latest";
    protected override string FileExtension => ".py";

    public override List<Command> GetExecutionPlan(
        string code, List<string>? packages = null)
    {
        var commands = new List<Command>
        {
            // 1. Write Python code
            new WriteFileCommand {
                Path = "/workspace/main.py",
                Content = code
            }
        };

        // 2. Install packages if needed
        if (packages != null && packages.Count > 0)
        {
            commands.Add(new ExecuteShellCommand {
                CommandName = "pip",
                Args = new List<string> { "install", "--no-cache-dir" }
                    .Concat(packages).ToList()
            });
        }

        // 3. Run Python script
        commands.Add(new ExecuteShellCommand {
            CommandName = "python3",
            Args = new List<string> { "/workspace/main.py" }
        });

        return commands;
    }
}
```

---

## 데이터 모델

### Session
```csharp
public sealed class Session
{
    public string SessionId { get; set; }          // GUID
    public string ContainerId { get; set; }        // Docker container ID
    public string Language { get; set; }           // python, javascript, go, csharp
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public SessionState State { get; set; }
    public SessionConfig Config { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public int ExecutionCount { get; set; }

    public bool IsExpired(DateTime now)
    {
        var idleTime = now - LastActivity;
        var lifetime = now - CreatedAt;
        return idleTime.TotalMinutes > Config.IdleTimeoutMinutes ||
               lifetime.TotalMinutes > Config.MaxLifetimeMinutes;
    }
}
```

### SessionConfig
```csharp
public sealed class SessionConfig
{
    public string Language { get; set; }
    public string? DockerImage { get; set; }
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int MaxLifetimeMinutes { get; set; } = 120;
    public bool PersistFilesystem { get; set; } = true;
    public long? MemoryLimitMB { get; set; }
    public long? CpuShares { get; set; }
}
```

### CommandResult
```csharp
public sealed class CommandResult
{
    public string? Id { get; set; }
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }

    public static CommandResult Ok(object? result = null, int durationMs = 0);
    public static CommandResult Fail(string error, int durationMs = 0);
}
```

---

## 성능 최적화

### 1. 컨테이너 재사용 (Session)
```
Before (Stateless):
- Container create: ~200ms
- Code execution: 100-500ms
- Container cleanup: ~100ms
- Total: 400-800ms per execution

After (Session):
- First execution: ~400ms (create session)
- Subsequent: ~100-200ms (reuse container)
- Improvement: 50-75% faster
```

### 2. Shell 우회 (Command System)
```
Before (Shell-based):
Request → "/bin/sh -c 'command'" → Parse → Execute
- Shell overhead: ~20-50ms

After (Direct API):
Request → Docker API → Direct Execute
- No shell parsing
- Improvement: ~20% faster
```

### 3. 병렬 처리
- **세션 풀링**: `ConcurrentDictionary` 기반
- **명령 실행**: `SemaphoreSlim` 동시성 제어
- **자동 정리**: Background worker (1분 간격)

---

## 보안 아키텍처

### 1. Docker 격리
```csharp
var createParams = new CreateContainerParameters
{
    Image = dockerImage,
    Cmd = new[] { "sleep", "infinity" },
    HostConfig = new HostConfig
    {
        Memory = 512 * 1024 * 1024,      // 512MB 제한
        CPUShares = 1024,                // CPU 제한
        NetworkMode = "none",            // 네트워크 격리
        AutoRemove = false               // 수동 관리
    }
};
```

### 2. 리소스 제한
- **메모리**: 기본 512MB (설정 가능)
- **CPU**: 기본 1024 shares
- **네트워크**: 격리 (`none`)
- **실행 시간**: ExecutionConfig.TimeoutSeconds

### 3. 세션 관리
- **IdleTimeout**: 30분 (기본)
- **MaxLifetime**: 120분 (기본)
- **자동 정리**: Background worker

---

## 확장성

### 수평 확장 (향후)
```
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│ API Server 1 │   │ API Server 2 │   │ API Server 3 │
└──────┬───────┘   └──────┬───────┘   └──────┬───────┘
       │                  │                  │
       └──────────────────┴──────────────────┘
                          │
                          ▼
                 ┌─────────────────┐
                 │ Redis (Sessions)│
                 └─────────────────┘
```

### 현재 제약사항
- **단일 인스턴스**: 세션이 메모리에 저장
- **로컬 Docker**: 단일 Docker 엔진

### 향후 개선
- [ ] Redis 기반 세션 스토리지
- [ ] 분산 Docker 오케스트레이션
- [ ] Kubernetes 배포
- [ ] 로드 밸런싱

---

## 모니터링 & 관리

### Health Check
```
GET http://localhost:5000/health
```

### 메트릭 (계획)
- 활성 세션 수
- 평균 실행 시간
- 에러율
- 컨테이너 리소스 사용량

### 로깅
- Serilog 통합 (Console + File)
- 구조화된 로그
- 에러 추적

---

## 참조 문서

- [Phase 1: JSON-RPC + WebSocket](PHASE1_COMPLETE.md)
- [Phase 2: Custom Commands](PHASE2_COMPLETE.md)
- [Phase 3: Session Management](PHASE3_COMPLETE.md)
- [통합 테스트](INTEGRATION_TESTS_COMPLETE.md)
- [프로덕션 준비](PRODUCTION_READY.md)

---

**CodeBeaker Architecture v1.0** - 2025-10-27

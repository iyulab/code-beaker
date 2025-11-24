# CodeBeaker C# Architecture

**마이그레이션 목표**: 고성능, 타입 안전, 엔터프라이즈급 코드 실행 플랫폼

---

## 1. 시스템 개요

### 핵심 원칙
- **파일시스템 기반**: Redis/PostgreSQL 불필요, 로컬 개발 친화적
- **Docker 격리**: 각 언어별 샌드박스 실행 환경
- **비동기 처리**: async/await 기반 고성능 워커 풀
- **타입 안전성**: 컴파일 타임 검증, 런타임 안정성

### 기술 스택
```
런타임:     .NET 8.0 (LTS)
웹 프레임워크: ASP.NET Core 8.0
테스트:     xUnit, FluentAssertions
벤치마크:   BenchmarkDotNet
컨테이너:   Docker, Docker SDK for .NET
```

---

## 2. 프로젝트 구조

```
CodeBeaker/
├── src/
│   ├── CodeBeaker.Core/              # 핵심 도메인 로직
│   │   ├── Models/
│   │   │   ├── ExecutionConfig.cs
│   │   │   ├── ExecutionResult.cs
│   │   │   └── TaskItem.cs
│   │   ├── Interfaces/
│   │   │   ├── IQueue.cs
│   │   │   ├── IStorage.cs
│   │   │   └── IRuntime.cs
│   │   ├── Queue/
│   │   │   └── FileQueue.cs          # 파일 기반 작업 큐
│   │   ├── Storage/
│   │   │   └── FileStorage.cs        # 파일 기반 상태 저장소
│   │   └── Docker/
│   │       └── DockerExecutor.cs     # Docker 컨테이너 실행
│   │
│   ├── CodeBeaker.Runtimes/          # 언어별 런타임 어댑터
│   │   ├── BaseRuntime.cs
│   │   ├── PythonRuntime.cs
│   │   ├── JavaScriptRuntime.cs
│   │   ├── GoRuntime.cs
│   │   ├── CSharpRuntime.cs
│   │   └── RuntimeRegistry.cs
│   │
│   ├── CodeBeaker.API/               # REST API 서버
│   │   ├── Controllers/
│   │   │   └── ExecuteController.cs
│   │   ├── Middleware/
│   │   │   ├── ErrorHandlingMiddleware.cs
│   │   │   └── RequestLoggingMiddleware.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── CodeBeaker.Worker/            # 백그라운드 워커
│       ├── WorkerService.cs          # IHostedService 구현
│       ├── WorkerPool.cs             # 병렬 워커 관리
│       └── Program.cs
│
├── tests/
│   ├── CodeBeaker.Core.Tests/
│   ├── CodeBeaker.Runtimes.Tests/
│   ├── CodeBeaker.API.Tests/
│   └── CodeBeaker.Integration.Tests/
│
├── docker/
│   └── runtimes/                     # 기존 유지
│
└── benchmarks/
    └── CodeBeaker.Benchmarks/
        └── ExecutionBenchmarks.cs
```

---

## 3. 핵심 컴포넌트 설계

### 3.1 FileQueue (파일 기반 큐)

**파일 구조**:
```
data/
└── queue/
    ├── pending/
    │   └── 20250126_120000_uuid.json
    └── processing/
        └── 20250126_120001_uuid.json
```

**C# 구현**:
```csharp
public class FileQueue : IQueue
{
    private readonly string _baseDir;
    private readonly string _pendingDir;
    private readonly string _processingDir;

    public async Task<string> SubmitTaskAsync(
        string code,
        string language,
        ExecutionConfig config,
        CancellationToken ct = default)
    {
        var executionId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffffff");
        var filename = $"{timestamp}_{executionId}.json";

        var task = new TaskItem
        {
            ExecutionId = executionId,
            Code = code,
            Language = language,
            Timeout = config.Timeout,
            MemoryLimit = config.MemoryLimit,
            CreatedAt = DateTime.UtcNow
        };

        // 원자적 쓰기: temp → rename
        var tempFile = Path.Combine(_pendingDir, $".tmp_{filename}");
        var targetFile = Path.Combine(_pendingDir, filename);

        await File.WriteAllTextAsync(
            tempFile,
            JsonSerializer.Serialize(task),
            ct);

        File.Move(tempFile, targetFile, overwrite: true);

        return executionId;
    }

    public async Task<TaskItem?> GetTaskAsync(
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var files = Directory
                .GetFiles(_pendingDir, "*.json")
                .OrderBy(f => f)
                .ToArray();

            if (files.Length == 0)
            {
                await Task.Delay(100, ct); // 100ms 폴링
                continue;
            }

            var taskFile = files[0];
            var processingFile = Path.Combine(
                _processingDir,
                Path.GetFileName(taskFile));

            try
            {
                File.Move(taskFile, processingFile, overwrite: true);
                var json = await File.ReadAllTextAsync(processingFile, ct);
                return JsonSerializer.Deserialize<TaskItem>(json);
            }
            catch (IOException)
            {
                // 다른 워커가 가져감
                continue;
            }
        }

        return null;
    }
}
```

---

### 3.2 FileStorage (파일 기반 저장소)

**파일 구조**:
```
data/
└── executions/
    └── {execution_id}/
        ├── status.json
        ├── stdout.txt
        └── stderr.txt
```

**C# 구현**:
```csharp
public class FileStorage : IStorage
{
    private readonly string _baseDir;

    public async Task SaveResultAsync(
        string executionId,
        ExecutionResult result,
        CancellationToken ct = default)
    {
        var execDir = Path.Combine(_baseDir, executionId);
        Directory.CreateDirectory(execDir);

        // Stdout/Stderr 저장
        await File.WriteAllTextAsync(
            Path.Combine(execDir, "stdout.txt"),
            result.Stdout,
            ct);

        await File.WriteAllTextAsync(
            Path.Combine(execDir, "stderr.txt"),
            result.Stderr,
            ct);

        // 상태 업데이트
        await UpdateStatusAsync(executionId, new
        {
            Status = result.ExitCode == 0 ? "completed" : "failed",
            ExitCode = result.ExitCode,
            DurationMs = result.DurationMs,
            Timeout = result.Timeout,
            CompletedAt = DateTime.UtcNow
        }, ct);
    }

    private async Task UpdateStatusAsync(
        string executionId,
        object statusUpdate,
        CancellationToken ct)
    {
        var execDir = Path.Combine(_baseDir, executionId);
        var statusFile = Path.Combine(execDir, "status.json");
        var tempFile = Path.Combine(execDir, ".tmp_status.json");

        // 원자적 쓰기
        await File.WriteAllTextAsync(
            tempFile,
            JsonSerializer.Serialize(statusUpdate),
            ct);

        File.Move(tempFile, statusFile, overwrite: true);
    }
}
```

---

### 3.3 DockerExecutor (Docker 실행)

**C# 구현**:
```csharp
public class DockerExecutor : IDisposable
{
    private readonly DockerClient _docker;

    public DockerExecutor()
    {
        _docker = new DockerClientConfiguration()
            .CreateClient();
    }

    public async Task<ExecutionResult> ExecuteAsync(
        string image,
        string[] command,
        string workspaceDir,
        ExecutionConfig config,
        CancellationToken ct = default)
    {
        var container = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = image,
                Cmd = command,
                HostConfig = new HostConfig
                {
                    Binds = new[] { $"{workspaceDir}:/workspace:ro" },
                    Memory = config.MemoryLimit * 1024 * 1024,
                    NanoCPUs = (long)(config.CpuLimit * 1_000_000_000),
                    NetworkMode = config.NetworkEnabled ? "bridge" : "none",
                    ReadonlyRootfs = true,
                    Tmpfs = new Dictionary<string, string>
                    {
                        ["/tmp"] = "rw,noexec,nosuid,size=100m"
                    }
                },
                User = "1000:1000"
            }, ct);

        var startTime = DateTime.UtcNow;

        await _docker.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters(),
            ct);

        // 타임아웃 처리
        using var cts = CancellationTokenSource
            .CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(config.Timeout));

        try
        {
            var waitResult = await _docker.Containers.WaitContainerAsync(
                container.ID,
                cts.Token);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // 로그 수집
            var logs = await GetContainerLogsAsync(container.ID, ct);

            await _docker.Containers.RemoveContainerAsync(
                container.ID,
                new ContainerRemoveParameters { Force = true },
                ct);

            return new ExecutionResult
            {
                ExitCode = (int)waitResult.StatusCode,
                Stdout = logs.Stdout,
                Stderr = logs.Stderr,
                DurationMs = (int)duration,
                Timeout = false
            };
        }
        catch (OperationCanceledException)
        {
            // 타임아웃 발생
            await _docker.Containers.KillContainerAsync(
                container.ID,
                new ContainerKillParameters(),
                CancellationToken.None);

            await _docker.Containers.RemoveContainerAsync(
                container.ID,
                new ContainerRemoveParameters { Force = true },
                CancellationToken.None);

            return new ExecutionResult
            {
                ExitCode = -1,
                Stdout = "",
                Stderr = $"Execution timeout after {config.Timeout} seconds",
                DurationMs = config.Timeout * 1000,
                Timeout = true
            };
        }
    }
}
```

---

### 3.4 RuntimeRegistry (런타임 관리)

**C# 구현**:
```csharp
public class RuntimeRegistry
{
    private static readonly Dictionary<string, IRuntime> _runtimes = new();

    public static void Register(string language, IRuntime runtime)
    {
        _runtimes[language.ToLower()] = runtime;
    }

    public static IRuntime Get(string language)
    {
        if (!_runtimes.TryGetValue(language.ToLower(), out var runtime))
            throw new NotSupportedException($"Language '{language}' not supported");

        return runtime;
    }

    public static string[] ListLanguages()
    {
        return _runtimes.Keys.ToArray();
    }

    static RuntimeRegistry()
    {
        // 런타임 등록
        Register("python", new PythonRuntime());
        Register("py", new PythonRuntime());
        Register("javascript", new JavaScriptRuntime());
        Register("js", new JavaScriptRuntime());
        Register("node", new JavaScriptRuntime());
        Register("go", new GoRuntime());
        Register("golang", new GoRuntime());
        Register("csharp", new CSharpRuntime());
        Register("cs", new CSharpRuntime());
    }
}
```

---

### 3.5 ASP.NET Core API

**Controller**:
```csharp
[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase
{
    private readonly IQueue _queue;
    private readonly IStorage _storage;

    [HttpPost]
    public async Task<IActionResult> Execute(
        [FromBody] ExecuteRequest request,
        CancellationToken ct)
    {
        // 동기 실행
        var executionId = await _queue.SubmitTaskAsync(
            request.Code,
            request.Language,
            request.Config,
            ct);

        // 워커가 처리할 때까지 대기 (폴링)
        var timeout = TimeSpan.FromSeconds(request.Config.Timeout + 5);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var status = await _storage.GetStatusAsync(executionId, ct);

            if (status?.Status is "completed" or "failed")
            {
                var result = await _storage.GetResultAsync(executionId, ct);
                return Ok(result);
            }

            await Task.Delay(100, ct);
        }

        return StatusCode(504, "Execution timeout");
    }

    [HttpPost("async")]
    public async Task<IActionResult> ExecuteAsync(
        [FromBody] ExecuteRequest request,
        CancellationToken ct)
    {
        // 비동기 실행 (큐만 제출)
        var executionId = await _queue.SubmitTaskAsync(
            request.Code,
            request.Language,
            request.Config,
            ct);

        return Accepted(new { executionId });
    }

    [HttpGet("status/{executionId}")]
    public async Task<IActionResult> GetStatus(
        string executionId,
        CancellationToken ct)
    {
        var result = await _storage.GetResultAsync(executionId, ct);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
}
```

---

### 3.6 Worker Service (백그라운드 처리)

**WorkerService**:
```csharp
public class CodeExecutionWorker : BackgroundService
{
    private readonly IQueue _queue;
    private readonly IStorage _storage;
    private readonly ILogger<CodeExecutionWorker> _logger;
    private readonly int _concurrency;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Worker starting with {Concurrency} concurrent executors", _concurrency);

        // 병렬 워커 실행
        var workers = Enumerable
            .Range(0, _concurrency)
            .Select(i => ProcessTasksAsync(i, ct));

        await Task.WhenAll(workers);
    }

    private async Task ProcessTasksAsync(int workerId, CancellationToken ct)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 큐에서 작업 가져오기 (1초 타임아웃)
                var task = await _queue.GetTaskAsync(
                    TimeSpan.FromSeconds(1),
                    ct);

                if (task == null)
                    continue;

                _logger.LogInformation(
                    "Worker {WorkerId} processing {ExecutionId}",
                    workerId,
                    task.ExecutionId);

                // 상태 업데이트: running
                await _storage.UpdateStatusAsync(
                    task.ExecutionId,
                    "running",
                    ct);

                // 런타임 실행
                var runtime = RuntimeRegistry.Get(task.Language);
                var config = new ExecutionConfig
                {
                    Timeout = task.Timeout,
                    MemoryLimit = task.MemoryLimit,
                    CpuLimit = task.CpuLimit,
                    NetworkEnabled = task.NetworkEnabled
                };

                var result = await runtime.ExecuteAsync(task.Code, config, ct);

                // 결과 저장
                await _storage.SaveResultAsync(task.ExecutionId, result, ct);

                _logger.LogInformation(
                    "Worker {WorkerId} completed {ExecutionId} (exit: {ExitCode})",
                    workerId,
                    task.ExecutionId,
                    result.ExitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} error", workerId);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }
}
```

---

## 4. 성능 최적화

### 4.1 비동기 I/O
- 모든 파일 I/O: `File.ReadAllTextAsync`, `File.WriteAllTextAsync`
- Docker API: Docker SDK의 async 메서드
- 큐 폴링: `Task.Delay` 사용

### 4.2 병렬 처리
- Worker 동시성: 환경 변수로 조정 (`WORKER_CONCURRENCY=20`)
- Task.WhenAll로 병렬 워커 실행
- SemaphoreSlim으로 리소스 제한

### 4.3 메모리 효율
- Span<T>, Memory<T> 사용
- 문자열 풀링: StringPool
- 객체 재사용: ObjectPool<T>

---

## 5. 설정 관리

**appsettings.json**:
```json
{
  "CodeBeaker": {
    "Queue": {
      "BaseDirectory": "data/queue",
      "PollingInterval": 100
    },
    "Storage": {
      "BaseDirectory": "data/executions"
    },
    "Worker": {
      "Concurrency": 10,
      "MaxRetries": 3
    },
    "Docker": {
      "DefaultTimeout": 30,
      "DefaultMemoryLimit": 512,
      "DefaultCpuLimit": 1.0
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "CodeBeaker": "Debug"
    }
  }
}
```

---

## 6. 테스트 전략

### 6.1 단위 테스트 (xUnit)
```csharp
public class FileQueueTests
{
    [Fact]
    public async Task SubmitTask_CreatesFileInPendingDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var queue = new FileQueue(tempDir);

        // Act
        var executionId = await queue.SubmitTaskAsync(
            "print('test')",
            "python",
            new ExecutionConfig());

        // Assert
        var pendingFiles = Directory.GetFiles(
            Path.Combine(tempDir, "pending"),
            "*.json");

        pendingFiles.Should().HaveCount(1);
    }
}
```

### 6.2 통합 테스트
```csharp
public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Execute_PythonCode_ReturnsCorrectOutput()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ExecuteRequest
        {
            Code = "print('Hello, World!')",
            Language = "python",
            Config = new ExecutionConfig { Timeout = 10 }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ExecutionResult>();
        result.Stdout.Should().Contain("Hello, World!");
    }
}
```

---

## 7. 배포

### 7.1 Docker Compose
```yaml
version: '3.8'

services:
  api:
    image: codebeaker-api:latest
    ports:
      - "5000:8080"
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - CodeBeaker__Worker__Concurrency=20

  worker:
    image: codebeaker-worker:latest
    volumes:
      - ./data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - CodeBeaker__Worker__Concurrency=10
    deploy:
      replicas: 3
```

### 7.2 Kubernetes (선택)
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: codebeaker-worker
spec:
  replicas: 5
  selector:
    matchLabels:
      app: codebeaker-worker
  template:
    spec:
      containers:
      - name: worker
        image: codebeaker-worker:latest
        env:
        - name: CodeBeaker__Worker__Concurrency
          value: "10"
        volumeMounts:
        - name: data
          mountPath: /app/data
```

---

## 8. 마이그레이션 체크리스트

- [ ] C# 프로젝트 생성 (.NET 8 솔루션)
- [ ] Core 라이브러리 구현 (FileQueue, FileStorage, Docker)
- [ ] Runtimes 구현 (Python, JS, Go, C# 어댑터)
- [ ] API 서버 구현 (ASP.NET Core)
- [ ] Worker 서비스 구현 (IHostedService)
- [ ] 단위 테스트 작성 (80% 커버리지)
- [ ] 통합 테스트 작성
- [ ] 성능 벤치마크 실행 (Python vs C# 비교)
- [ ] Docker 이미지 빌드
- [ ] 문서 업데이트
- [ ] Python 코드 아카이브 (`archive/python-legacy/`)

---

## 9. 성공 지표

| 항목 | 목표 |
|------|------|
| API 응답 시간 (p99) | < 5ms |
| 워커 처리량 | > 200 req/s |
| 메모리 사용 (API) | < 100MB |
| 테스트 커버리지 | > 80% |
| Docker 빌드 시간 | < 2분 |
| 동시 워커 수 | > 50개 |

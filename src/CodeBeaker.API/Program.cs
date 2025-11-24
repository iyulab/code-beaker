using System.Reflection;
using System.Text.Json;
using CodeBeaker.API.Health;
using CodeBeaker.API.JsonRpc.Handlers;
using CodeBeaker.API.Metrics;
using CodeBeaker.API.WebSocket;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Queue;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using CodeBeaker.JsonRpc;
using CodeBeaker.JsonRpc.Handlers;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Docker;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Events;

// Phase 8: Structured logging with Serilog
var machineName = Environment.MachineName;
var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
var aspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CodeBeaker")
    .Enrich.WithProperty("Version", appVersion)
    .Enrich.WithProperty("Environment", aspNetCoreEnv)
    .Enrich.WithProperty("MachineName", machineName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/codebeaker-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{MachineName}] [{Application}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting CodeBeaker API");

    var builder = WebApplication.CreateBuilder(args);

    // Phase 8: Use Serilog for logging
    builder.Host.UseSerilog();

    // 설정 로드
    var queuePath = builder.Configuration.GetValue<string>("Queue:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-queue");
    var storagePath = builder.Configuration.GetValue<string>("Storage:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-storage");

    // 의존성 주입 설정
    builder.Services.AddSingleton<IQueue>(sp => new FileQueue(queuePath));
    builder.Services.AddSingleton<IStorage>(sp => new FileStorage(storagePath));

    // Docker Client 등록 (Phase 6.3 - Docker 관련 서비스 공유)
    builder.Services.AddSingleton<Docker.DotNet.DockerClient>(sp =>
    {
        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        return new Docker.DotNet.DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
    });

    // Multi-Runtime 등록
    builder.Services.AddSingleton<IExecutionRuntime>(sp => new DockerRuntime());
    builder.Services.AddSingleton<IExecutionRuntime>(sp => new DenoRuntime());
    builder.Services.AddSingleton<IExecutionRuntime>(sp => new BunRuntime());
    builder.Services.AddSingleton<IExecutionRuntime>(sp => new CodeBeaker.Runtimes.Node.NodeRuntime()); // Phase 9.1
    builder.Services.AddSingleton<IExecutionRuntime>(sp => new CodeBeaker.Runtimes.Python.PythonRuntime()); // Phase 9.2

    // Session Store 등록 (InMemory 기본, 환경 변수로 Redis 전환 가능)
    var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        // Redis 사용 (분산 환경)
        builder.Services.AddSingleton<ISessionStore>(sp =>
        {
            var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
            return new RedisSessionStore(redis);
        });
        builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
        {
            return StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
        });
    }
    else
    {
        // InMemory 사용 (단일 인스턴스)
        builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
    }

    // SessionManager에 ISessionStore + Multi-Runtime 주입
    builder.Services.AddSingleton<ISessionManager>(sp =>
    {
        var sessionStore = sp.GetRequiredService<ISessionStore>();
        var runtimes = sp.GetServices<IExecutionRuntime>();
        return new SessionManager(sessionStore, runtimes);
    });

    // Phase 7: Command Result Cache (optional)
    builder.Services.AddSingleton(sp => new CodeBeaker.Core.Caching.CommandResultCache(
        defaultExpiration: TimeSpan.FromMinutes(5),
        maxCacheSize: 1000));

    // Background services
    builder.Services.AddHostedService<SessionCleanupWorker>();

    // Phase 6.3: Graceful Shutdown & Stability
    builder.Services.AddHostedService<CodeBeaker.Core.Services.GracefulShutdownService>();
    builder.Services.AddHostedService<CodeBeaker.Runtimes.Docker.DockerConnectionMonitor>();
    builder.Services.AddHostedService<CodeBeaker.Runtimes.Docker.DockerCleanupService>();

    // Phase 8: Metrics Collection
    builder.Services.AddHostedService<CodeBeaker.API.Metrics.MetricsCollectionService>();

    // JSON-RPC 설정
    builder.Services.AddSingleton(sp =>
    {
        var router = new JsonRpcRouter();

        // Register handlers
        var sessionManager = sp.GetRequiredService<ISessionManager>();

        var handlers = new CodeBeaker.JsonRpc.Interfaces.IJsonRpcHandler[]
        {
        new InitializeHandler(),
        new ExecutionRunHandler(
            sp.GetRequiredService<IQueue>(),
            sp.GetRequiredService<IStorage>(),
            sp.GetRequiredService<ILogger<ExecutionRunHandler>>()
        ),
        new ExecutionStatusHandler(
            sp.GetRequiredService<IStorage>(),
            sp.GetRequiredService<ILogger<ExecutionStatusHandler>>()
        ),
        new LanguageListHandler(),
        // Session handlers
        new SessionCreateHandler(sessionManager),
        new SessionExecuteHandler(sessionManager),
        new SessionCloseHandler(sessionManager),
        new SessionListHandler(sessionManager)
        };

        router.RegisterHandlers(handlers);

        return router;
    });

    builder.Services.AddSingleton<WebSocketHandler>();
    builder.Services.AddSingleton<StreamingExecutor>();

    // 컨트롤러 추가
    builder.Services.AddControllers();

    // CORS 설정 (개발 환경)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Swagger/OpenAPI 설정
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "CodeBeaker API",
            Version = "v1",
            Description = "안전하고 빠른 코드 실행 플랫폼 API",
            Contact = new OpenApiContact
            {
                Name = "CodeBeaker",
                Url = new Uri("https://github.com/yourusername/code-beaker")
            },
            License = new OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        });

        // XML 문서 주석 포함
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // 헬스체크 추가
    builder.Services.AddHealthChecks()
        .AddCheck<RuntimeHealthCheck>(
            "runtime",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "ready" })
        .AddCheck<SessionManagerHealthCheck>(
            "session_manager",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "ready" })
        .AddCheck<StorageHealthCheck>(
            "storage",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "ready" })
        .AddCheck<QueueHealthCheck>(
            "queue",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "ready" });

    var app = builder.Build();

    // HTTP 요청 파이프라인 설정
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeBeaker API v1");
            options.RoutePrefix = string.Empty; // Swagger UI를 루트에 배치
        });
        app.UseCors("AllowAll");
    }

    // WebSocket 설정
    var webSocketOptions = new WebSocketOptions
    {
        KeepAliveInterval = TimeSpan.FromMinutes(2)
    };
    app.UseWebSockets(webSocketOptions);

    // WebSocket endpoint for JSON-RPC
    app.Map("/ws/jsonrpc", async (HttpContext context) =>
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
            await handler.HandleConnectionAsync(webSocket, context.RequestAborted);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    });

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // Kubernetes 스타일 Health Check 엔드포인트
    // Liveness probe - 프로세스가 살아있는지 확인 (간단한 응답)
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false, // 모든 헬스체크 스킵 (기본 liveness만)
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    });

    // Readiness probe - 트래픽 받을 준비가 되었는지 확인
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                timestamp = DateTimeOffset.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data
                })
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    });

    // Startup probe - 초기 시작 완료 확인 (readiness와 동일하지만 실패 허용도가 높음)
    app.MapHealthChecks("/health/startup", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                timestamp = DateTimeOffset.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString().ToLowerInvariant()
                })
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    });

    // 전체 상태 확인 (디버깅용, 상세 정보 포함)
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                timestamp = DateTimeOffset.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                })
            };
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
    });

    // Prometheus metrics 활성화
    app.UseMetricServer(); // /metrics 엔드포인트
    app.UseHttpMetrics(); // HTTP request metrics

    // 초기화 메트릭 기록
    CodeBeakerMetrics.RecordRestart();

    // 메모리 메트릭 주기 업데이트 (백그라운드 타이머)
    var metricsTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
    metricsTimer.Elapsed += (sender, e) => CodeBeakerMetrics.UpdateMemoryMetrics();
    metricsTimer.Start();

    // 시작 로그
    app.Logger.LogInformation("CodeBeaker API starting...");
    app.Logger.LogInformation("Queue path: {QueuePath}", queuePath);
    app.Logger.LogInformation("Storage path: {StoragePath}", storagePath);
    app.Logger.LogInformation("WebSocket endpoint: /ws/jsonrpc");
    app.Logger.LogInformation("Metrics endpoint: /metrics");

    // Multi-Runtime 정보 로그 및 메트릭 업데이트
    var runtimes = app.Services.GetServices<IExecutionRuntime>();
    app.Logger.LogInformation("Registered Runtimes:");
    foreach (var runtime in runtimes)
    {
        var available = await runtime.IsAvailableAsync();
        var capabilities = runtime.GetCapabilities();
        app.Logger.LogInformation(
            "  - {Name} ({Type}): Available={Available}, Startup={StartupMs}ms, Isolation={Isolation}/10",
            runtime.Name,
            runtime.Type,
            available,
            capabilities.StartupTimeMs,
            capabilities.IsolationLevel
        );

        // 런타임 가용성 메트릭 업데이트
        CodeBeakerMetrics.RuntimesAvailable
            .WithLabels(runtime.Type.ToString().ToLowerInvariant())
            .Set(available ? 1 : 0);
    }

    app.Run();

    Log.Information("CodeBeaker API stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "CodeBeaker API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// WebApplicationFactory를 위한 public partial Program 클래스
public partial class Program { }

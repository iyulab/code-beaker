using System.Reflection;
using CodeBeaker.API.JsonRpc.Handlers;
using CodeBeaker.API.WebSocket;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Queue;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using CodeBeaker.JsonRpc;
using CodeBeaker.JsonRpc.Handlers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 설정 로드
var queuePath = builder.Configuration.GetValue<string>("Queue:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-queue");
var storagePath = builder.Configuration.GetValue<string>("Storage:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-storage");

// 의존성 주입 설정
builder.Services.AddSingleton<IQueue>(sp => new FileQueue(queuePath));
builder.Services.AddSingleton<IStorage>(sp => new FileStorage(storagePath));
builder.Services.AddSingleton<ISessionManager, SessionManager>();

// Background services
builder.Services.AddHostedService<SessionCleanupWorker>();

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
builder.Services.AddHealthChecks();

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
app.MapHealthChecks("/health");

// 시작 로그
app.Logger.LogInformation("CodeBeaker API starting...");
app.Logger.LogInformation("Queue path: {QueuePath}", queuePath);
app.Logger.LogInformation("Storage path: {StoragePath}", storagePath);
app.Logger.LogInformation("WebSocket endpoint: /ws/jsonrpc");

app.Run();

// WebApplicationFactory를 위한 public partial Program 클래스
public partial class Program { }

using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Queue;
using CodeBeaker.Core.Storage;
using CodeBeaker.Worker;

var builder = Host.CreateApplicationBuilder(args);

// 설정 로드
var queuePath = builder.Configuration.GetValue<string>("Queue:Path")
    ?? Path.Combine(Path.GetTempPath(), "codebeaker-queue");
var storagePath = builder.Configuration.GetValue<string>("Storage:Path")
    ?? Path.Combine(Path.GetTempPath(), "codebeaker-storage");

// 의존성 주입 설정
builder.Services.AddSingleton<IQueue>(sp => new FileQueue(queuePath));
builder.Services.AddSingleton<IStorage>(sp => new FileStorage(storagePath));

// Worker 서비스 추가
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// 시작 로그
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("CodeBeaker Worker starting...");
logger.LogInformation("Queue path: {QueuePath}", queuePath);
logger.LogInformation("Storage path: {StoragePath}", storagePath);

host.Run();

using System.Reflection;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Queue;
using CodeBeaker.Core.Storage;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 설정 로드
var queuePath = builder.Configuration.GetValue<string>("Queue:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-queue");
var storagePath = builder.Configuration.GetValue<string>("Storage:Path") ?? Path.Combine(Path.GetTempPath(), "codebeaker-storage");

// 의존성 주입 설정
builder.Services.AddSingleton<IQueue>(sp => new FileQueue(queuePath));
builder.Services.AddSingleton<IStorage>(sp => new FileStorage(storagePath));

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// 시작 로그
app.Logger.LogInformation("CodeBeaker API starting...");
app.Logger.LogInformation("Queue path: {QueuePath}", queuePath);
app.Logger.LogInformation("Storage path: {StoragePath}", storagePath);

app.Run();

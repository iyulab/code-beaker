using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Queue;
using CodeBeaker.Core.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeBeaker.Integration.Tests.TestHelpers;

/// <summary>
/// API 통합 테스트를 위한 Fixture
/// WebApplicationFactory를 사용하여 in-memory API 서버 생성
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>
{
    public string QueuePath { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 테스트용 임시 디렉토리 생성
        QueuePath = Path.Combine(Path.GetTempPath(), $"codebeaker-test-queue-{Guid.NewGuid()}");
        StoragePath = Path.Combine(Path.GetTempPath(), $"codebeaker-test-storage-{Guid.NewGuid()}");

        Directory.CreateDirectory(Path.Combine(QueuePath, "pending"));
        Directory.CreateDirectory(Path.Combine(QueuePath, "processing"));
        Directory.CreateDirectory(Path.Combine(QueuePath, "completed"));
        Directory.CreateDirectory(StoragePath);

        builder.ConfigureServices(services =>
        {
            // 기존 서비스 제거
            var queueDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IQueue));
            if (queueDescriptor != null)
                services.Remove(queueDescriptor);

            var storageDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IStorage));
            if (storageDescriptor != null)
                services.Remove(storageDescriptor);

            // 테스트용 서비스 등록
            services.AddSingleton<IQueue>(sp => new FileQueue(QueuePath));
            services.AddSingleton<IStorage>(sp => new FileStorage(StoragePath));
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 테스트 종료 후 임시 디렉토리 정리
            try
            {
                if (Directory.Exists(QueuePath))
                    Directory.Delete(QueuePath, true);
                if (Directory.Exists(StoragePath))
                    Directory.Delete(StoragePath, true);
            }
            catch
            {
                // 정리 실패는 무시
            }
        }

        base.Dispose(disposing);
    }
}

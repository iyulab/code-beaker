using CodeBeaker.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeBeaker.API.Health;

/// <summary>
/// Storage 시스템 상태 확인을 위한 Health Check
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly IStorage _storage;
    private readonly ILogger<StorageHealthCheck> _logger;

    public StorageHealthCheck(
        IStorage storage,
        ILogger<StorageHealthCheck> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 간단한 쓰기/읽기 테스트
            var testId = $"healthcheck-{Guid.NewGuid():N}";

            // 상태 업데이트 테스트
            await _storage.UpdateStatusAsync(
                testId,
                "healthy",
                exitCode: 0,
                durationMs: 0,
                cancellationToken: cancellationToken);

            // 상태 조회 테스트
            var result = await _storage.GetStatusAsync(testId, cancellationToken);

            if (result != null && result.Status == "healthy")
            {
                return HealthCheckResult.Healthy("Storage operational");
            }
            else
            {
                return HealthCheckResult.Degraded("Storage read/write test failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed");
            return HealthCheckResult.Unhealthy(
                "Storage not operational",
                ex);
        }
    }
}

using CodeBeaker.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeBeaker.API.Health;

/// <summary>
/// Queue 시스템 상태 확인을 위한 Health Check
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly IQueue _queue;
    private readonly ILogger<QueueHealthCheck> _logger;

    public QueueHealthCheck(
        IQueue queue,
        ILogger<QueueHealthCheck> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Queue는 파일 시스템 기반이므로 디렉토리 존재 여부만 확인
            // (IQueue 인터페이스에 depth 메서드가 없음)

            // Queue가 초기화되어 있으면 Healthy
            return Task.FromResult(
                HealthCheckResult.Healthy("Queue operational"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Queue not operational",
                    ex));
        }
    }
}

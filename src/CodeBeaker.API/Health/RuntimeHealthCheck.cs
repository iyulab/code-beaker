using CodeBeaker.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeBeaker.API.Health;

/// <summary>
/// 런타임 가용성 확인을 위한 Health Check
/// </summary>
public class RuntimeHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IExecutionRuntime> _runtimes;
    private readonly ILogger<RuntimeHealthCheck> _logger;

    public RuntimeHealthCheck(
        IEnumerable<IExecutionRuntime> runtimes,
        ILogger<RuntimeHealthCheck> logger)
    {
        _runtimes = runtimes;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runtimeStatuses = new Dictionary<string, object>();
            var availableCount = 0;
            var totalCount = 0;

            foreach (var runtime in _runtimes)
            {
                totalCount++;
                var isAvailable = await runtime.IsAvailableAsync();

                runtimeStatuses[runtime.Name] = new
                {
                    Available = isAvailable,
                    Type = runtime.Type
                };

                if (isAvailable)
                {
                    availableCount++;
                }
            }

            // 최소 1개 이상의 런타임이 사용 가능해야 Healthy
            if (availableCount > 0)
            {
                return HealthCheckResult.Healthy(
                    $"{availableCount}/{totalCount} runtimes available",
                    runtimeStatuses);
            }
            else if (totalCount > 0)
            {
                return HealthCheckResult.Degraded(
                    "No runtimes available",
                    data: runtimeStatuses);
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    "No runtimes registered",
                    data: runtimeStatuses);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime health check failed");
            return HealthCheckResult.Unhealthy(
                "Runtime health check failed",
                ex);
        }
    }
}

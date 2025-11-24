using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Core.Monitoring;

/// <summary>
/// IExecutionEnvironment 기반 리소스 모니터
/// </summary>
public sealed class EnvironmentResourceMonitor : IResourceMonitor
{
    private readonly IExecutionEnvironment _environment;
    private readonly List<ResourceUsage> _usageHistory = new();
    private readonly int _maxHistorySize;

    public EnvironmentResourceMonitor(IExecutionEnvironment environment, int maxHistorySize = 100)
    {
        _environment = environment;
        _maxHistorySize = maxHistorySize;
    }

    public async Task<ResourceUsage> GetCurrentUsageAsync(CancellationToken cancellationToken = default)
    {
        var usage = await _environment.GetResourceUsageAsync(cancellationToken);

        if (usage == null)
        {
            // Fallback: 빈 usage 반환
            usage = new ResourceUsage
            {
                Timestamp = DateTime.UtcNow
            };
        }

        // 히스토리 저장
        lock (_usageHistory)
        {
            _usageHistory.Add(usage);

            // 최대 크기 초과 시 오래된 항목 제거
            if (_usageHistory.Count > _maxHistorySize)
            {
                _usageHistory.RemoveAt(0);
            }
        }

        return usage;
    }

    public async Task<ResourceViolation?> CheckViolationsAsync(
        ResourceLimits limits,
        CancellationToken cancellationToken = default)
    {
        var usage = await GetCurrentUsageAsync(cancellationToken);

        // Memory Limit 체크
        if (limits.MemoryLimitBytes.HasValue &&
            usage.MemoryUsageBytes > limits.MemoryLimitBytes.Value)
        {
            var severity = (double)usage.MemoryUsageBytes / limits.MemoryLimitBytes.Value - 1.0;
            return new ResourceViolation
            {
                Timestamp = DateTime.UtcNow,
                Type = ResourceViolationType.MemoryLimit,
                CurrentValue = usage.MemoryUsageBytes,
                LimitValue = limits.MemoryLimitBytes.Value,
                Severity = Math.Min(severity, 1.0),
                Message = $"Memory limit exceeded: {usage.MemoryUsageBytes:N0} bytes > {limits.MemoryLimitBytes.Value:N0} bytes",
                ShouldTerminate = true // 메모리 하드 리미트는 종료 필요
            };
        }

        // Memory Warning 체크
        if (limits.MemoryWarningBytes.HasValue &&
            usage.MemoryUsageBytes > limits.MemoryWarningBytes.Value)
        {
            var severity = (double)usage.MemoryUsageBytes / limits.MemoryWarningBytes.Value - 1.0;
            return new ResourceViolation
            {
                Timestamp = DateTime.UtcNow,
                Type = ResourceViolationType.MemoryWarning,
                CurrentValue = usage.MemoryUsageBytes,
                LimitValue = limits.MemoryWarningBytes.Value,
                Severity = Math.Min(severity, 1.0),
                Message = $"Memory warning threshold exceeded: {usage.MemoryUsageBytes:N0} bytes > {limits.MemoryWarningBytes.Value:N0} bytes",
                ShouldTerminate = false // 경고만
            };
        }

        // CPU Quota 체크 (실시간 CPU% 기반)
        if (limits.CpuQuotaMicroseconds.HasValue && limits.CpuPeriodMicroseconds.HasValue)
        {
            // CpuQuota / CpuPeriod = 허용된 CPU 코어 비율
            var allowedCpuPercent = (double)limits.CpuQuotaMicroseconds.Value / limits.CpuPeriodMicroseconds.Value * 100.0;

            if (usage.CpuUsagePercent > allowedCpuPercent)
            {
                var severity = usage.CpuUsagePercent / allowedCpuPercent - 1.0;
                return new ResourceViolation
                {
                    Timestamp = DateTime.UtcNow,
                    Type = ResourceViolationType.CpuQuota,
                    CurrentValue = (long)usage.CpuUsagePercent,
                    LimitValue = (long)allowedCpuPercent,
                    Severity = Math.Min(severity, 1.0),
                    Message = $"CPU quota exceeded: {usage.CpuUsagePercent:F2}% > {allowedCpuPercent:F2}%",
                    ShouldTerminate = false // CPU 스로틀링은 Docker가 자동 처리
                };
            }
        }

        // Disk Quota 체크
        if (limits.DiskQuotaBytes.HasValue &&
            usage.DiskUsageBytes > limits.DiskQuotaBytes.Value)
        {
            var severity = (double)usage.DiskUsageBytes / limits.DiskQuotaBytes.Value - 1.0;
            return new ResourceViolation
            {
                Timestamp = DateTime.UtcNow,
                Type = ResourceViolationType.DiskQuota,
                CurrentValue = usage.DiskUsageBytes,
                LimitValue = limits.DiskQuotaBytes.Value,
                Severity = Math.Min(severity, 1.0),
                Message = $"Disk quota exceeded: {usage.DiskUsageBytes:N0} bytes > {limits.DiskQuotaBytes.Value:N0} bytes",
                ShouldTerminate = true // 디스크 쿼터 초과 시 종료
            };
        }

        // Process Count 체크
        if (limits.MaxProcesses.HasValue &&
            usage.ProcessCount > limits.MaxProcesses.Value)
        {
            var severity = (double)usage.ProcessCount / limits.MaxProcesses.Value - 1.0;
            return new ResourceViolation
            {
                Timestamp = DateTime.UtcNow,
                Type = ResourceViolationType.ProcessCount,
                CurrentValue = usage.ProcessCount,
                LimitValue = limits.MaxProcesses.Value,
                Severity = Math.Min(severity, 1.0),
                Message = $"Process count exceeded: {usage.ProcessCount} > {limits.MaxProcesses.Value}",
                ShouldTerminate = true // 프로세스 개수 초과 시 종료
            };
        }

        // 위반 없음
        return null;
    }

    public Task<List<ResourceUsage>> GetUsageHistoryAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        lock (_usageHistory)
        {
            var history = _usageHistory
                .TakeLast(count)
                .ToList();

            return Task.FromResult(history);
        }
    }
}

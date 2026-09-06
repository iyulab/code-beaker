using System.Collections.Concurrent;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Core.Monitoring;

/// <summary>
/// 백그라운드 리소스 모니터링 서비스
/// 주기적으로 활성 환경의 리소스 사용량을 체크하고 위반 시 자동 종료
/// </summary>
public sealed class ResourceMonitoringService : BackgroundService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ResourceMonitoringService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly bool _enableAutoTermination;

    /// <summary>
    /// 세션별 리소스 모니터. 세션 수명 동안 유지해야 사용 이력이 쌓인다.
    /// </summary>
    private readonly ConcurrentDictionary<string, EnvironmentResourceMonitor> _monitors = new();

    public ResourceMonitoringService(
        ISessionManager sessionManager,
        ILogger<ResourceMonitoringService> logger,
        TimeSpan? checkInterval = null,
        bool enableAutoTermination = true)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(5);
        _enableAutoTermination = enableAutoTermination;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Resource monitoring service started (interval: {Interval}s, auto-terminate: {AutoTerminate})",
            _checkInterval.TotalSeconds,
            _enableAutoTermination);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource monitoring check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Resource monitoring service stopped");
    }

    private async Task CheckAllSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

        // 사라진 세션의 모니터는 이력과 함께 버린다.
        var liveSessionIds = sessions.Select(s => s.SessionId).ToHashSet(StringComparer.Ordinal);
        foreach (var trackedId in _monitors.Keys)
        {
            if (!liveSessionIds.Contains(trackedId))
            {
                _monitors.TryRemove(trackedId, out _);
            }
        }

        foreach (var session in sessions)
        {
            try
            {
                await CheckSessionResourcesAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking resources for session {SessionId}",
                    session.SessionId);
            }
        }
    }

    internal async Task CheckSessionResourcesAsync(Session session, CancellationToken cancellationToken)
    {
        // Environment가 null이면 스킵 (환경이 아직 생성되지 않았거나 정리됨)
        if (session.Environment == null)
        {
            return;
        }

        // 환경 상태 확인
        var state = await session.Environment.GetStateAsync(cancellationToken);
        if (state == EnvironmentState.Stopped || state == EnvironmentState.Error)
        {
            _monitors.TryRemove(session.SessionId, out _);
            return;
        }

        // 세션 수명 동안 같은 모니터를 재사용해야 사용 이력이 누적된다.
        var monitor = _monitors.GetOrAdd(
            session.SessionId,
            _ => new EnvironmentResourceMonitor(session.Environment));

        var limits = BuildLimits(session.Config);
        var violation = await monitor.CheckViolationsAsync(limits, cancellationToken);

        if (violation == null)
        {
            var usage = await monitor.GetCurrentUsageAsync(cancellationToken);
            _logger.LogDebug(
                "Resource usage for session {SessionId}: Memory={MemoryMB:N0}MB, CPU={CpuPercent:F1}%, Processes={ProcessCount}",
                session.SessionId,
                usage.MemoryUsageBytes / (1024 * 1024),
                usage.CpuUsagePercent,
                usage.ProcessCount);
            return;
        }

        if (!violation.ShouldTerminate)
        {
            _logger.LogWarning(
                "Session {SessionId} resource warning ({ViolationType}): {Message}",
                session.SessionId,
                violation.Type,
                violation.Message);
            return;
        }

        if (!_enableAutoTermination)
        {
            _logger.LogWarning(
                "Session {SessionId} exceeded a hard resource limit ({ViolationType}) but auto-termination is disabled: {Message}",
                session.SessionId,
                violation.Type,
                violation.Message);
            return;
        }

        _logger.LogError(
            "Terminating session {SessionId}: {Message}",
            session.SessionId,
            violation.Message);

        await _sessionManager.CloseSessionAsync(session.SessionId, cancellationToken);
        _monitors.TryRemove(session.SessionId, out _);
    }

    /// <summary>
    /// SessionConfig에 표명된 제한을 위반 판정용 ResourceLimits로 옮긴다.
    /// (SessionManager가 RuntimeConfig를 만들 때 쓰는 것과 같은 매핑)
    /// </summary>
    private static ResourceLimits BuildLimits(SessionConfig config)
    {
        return new ResourceLimits
        {
            MemoryLimitMB = config.MemoryLimitMB,
            CpuShares = config.CpuShares
        };
    }
}

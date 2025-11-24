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

    private async Task CheckSessionResourcesAsync(Session session, CancellationToken cancellationToken)
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
            return;
        }

        // 리소스 사용량 조회 (단순 로깅 목적)
        var usage = await session.Environment.GetResourceUsageAsync(cancellationToken);

        if (usage == null)
        {
            _logger.LogDebug(
                "Unable to retrieve resource usage for session {SessionId}",
                session.SessionId);
            return;
        }

        // Phase 6.2: 기본 리소스 사용량 로깅
        // TODO: SessionConfig에 ResourceLimits 추가 시 위반 감지 및 자동 종료 구현
        _logger.LogDebug(
            "Resource usage for session {SessionId}: Memory={MemoryMB:N0}MB, CPU={CpuPercent:F1}%, Processes={ProcessCount}",
            session.SessionId,
            usage.MemoryUsageBytes / (1024 * 1024),
            usage.CpuUsagePercent,
            usage.ProcessCount);

        // 기본 메모리 제한 체크 (SessionConfig.MemoryLimitMB 사용)
        if (session.Config.MemoryLimitMB.HasValue)
        {
            var memoryLimitBytes = session.Config.MemoryLimitMB.Value * 1024 * 1024;
            if (usage.MemoryUsageBytes > memoryLimitBytes)
            {
                _logger.LogWarning(
                    "Session {SessionId} exceeded memory limit: {UsageMB:N0}MB > {LimitMB:N0}MB",
                    session.SessionId,
                    usage.MemoryUsageBytes / (1024 * 1024),
                    session.Config.MemoryLimitMB.Value);

                // Phase 6.2: 자동 종료는 보류 (추가 검증 및 통합 후 활성화)
                // if (_enableAutoTermination)
                // {
                //     await session.Environment.CleanupAsync(cancellationToken);
                // }
            }
        }
    }
}

using CodeBeaker.Core.Caching;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.API.Metrics;

/// <summary>
/// Phase 8: 주기적 메트릭 수집 서비스
/// 리소스 사용량, 캐시 통계, 세션 상태를 Prometheus로 노출
/// </summary>
public sealed class MetricsCollectionService : BackgroundService
{
    private readonly ISessionManager _sessionManager;
    private readonly CommandResultCache? _cache;
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly TimeSpan _collectionInterval;

    public MetricsCollectionService(
        ISessionManager sessionManager,
        ILogger<MetricsCollectionService> logger,
        CommandResultCache? cache = null,
        TimeSpan? collectionInterval = null)
    {
        _sessionManager = sessionManager;
        _cache = cache;
        _logger = logger;
        _collectionInterval = collectionInterval ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsCollectionService started with {Interval}s interval", _collectionInterval.TotalSeconds);

        // 서비스 헬스 초기화
        CodeBeakerMetrics.BackgroundServiceHealth.WithLabels("MetricsCollectionService").Set(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
                await Task.Delay(_collectionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
                CodeBeakerMetrics.BackgroundServiceErrors
                    .WithLabels("MetricsCollectionService", ex.GetType().Name)
                    .Inc();

                // 에러 발생 시에도 계속 실행
                await Task.Delay(_collectionInterval, stoppingToken);
            }
        }

        CodeBeakerMetrics.BackgroundServiceHealth.WithLabels("MetricsCollectionService").Set(0);
        _logger.LogInformation("MetricsCollectionService stopped");
    }

    private async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        // 1. 세션 메트릭 수집
        await CollectSessionMetricsAsync(cancellationToken);

        // 2. 캐시 메트릭 수집
        CollectCacheMetrics();

        // 3. 프로세스 메트릭 수집
        CollectProcessMetrics();
    }

    private async Task CollectSessionMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

            // 활성 세션 수 업데이트 (런타임별)
            var sessionsByRuntime = sessions.GroupBy(s => s.RuntimeType);
            foreach (var group in sessionsByRuntime)
            {
                CodeBeakerMetrics.ActiveSessions
                    .WithLabels(group.Key.ToString())
                    .Set(group.Count());
            }

            // Phase 8.1: 세션별 리소스 사용량 수집
            foreach (var session in sessions)
            {
                try
                {
                    var usage = await _sessionManager.GetSessionResourceUsageAsync(
                        session.SessionId,
                        cancellationToken);

                    if (usage == null)
                    {
                        // 리소스 모니터링을 지원하지 않는 런타임
                        continue;
                    }

                    // 메모리 사용량
                    CodeBeakerMetrics.SessionMemoryUsage
                        .WithLabels(session.SessionId, session.RuntimeType.ToString())
                        .Set(usage.MemoryUsageBytes);

                    // CPU 사용률
                    CodeBeakerMetrics.SessionCpuUsage
                        .WithLabels(session.SessionId, session.RuntimeType.ToString())
                        .Set(usage.CpuUsagePercent);

                    // 디스크 사용량
                    if (usage.DiskUsageBytes > 0)
                    {
                        CodeBeakerMetrics.SessionDiskUsage
                            .WithLabels(session.SessionId, session.RuntimeType.ToString())
                            .Set(usage.DiskUsageBytes);
                    }

                    // 네트워크 사용량
                    if (usage.NetworkRxBytes > 0)
                    {
                        CodeBeakerMetrics.SessionNetworkRx
                            .WithLabels(session.SessionId, session.RuntimeType.ToString())
                            .Set(usage.NetworkRxBytes);
                    }

                    if (usage.NetworkTxBytes > 0)
                    {
                        CodeBeakerMetrics.SessionNetworkTx
                            .WithLabels(session.SessionId, session.RuntimeType.ToString())
                            .Set(usage.NetworkTxBytes);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to collect resource metrics for session {SessionId}", session.SessionId);
                    // 개별 세션 실패는 무시하고 계속 진행
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting session metrics");
            throw;
        }
    }

    private void CollectCacheMetrics()
    {
        if (_cache == null)
        {
            return;
        }

        try
        {
            var stats = _cache.GetStatistics();

            // 캐시 통계를 Prometheus 메트릭으로 노출
            CodeBeakerMetrics.CacheSize
                .WithLabels("command_result")
                .Set(stats.CacheSize);

            CodeBeakerMetrics.CacheHitRate
                .WithLabels("command_result")
                .Set(stats.HitRate);

            // Note: 캐시 히트/미스 카운터는 CommandResultCache에서 직접 증가
            // 여기서는 Gauge 메트릭만 업데이트
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting cache metrics");
            throw;
        }
    }

    private void CollectProcessMetrics()
    {
        try
        {
            CodeBeakerMetrics.UpdateMemoryMetrics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting process metrics");
            throw;
        }
    }
}

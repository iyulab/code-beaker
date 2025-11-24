using CodeBeaker.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Core.Sessions;

/// <summary>
/// 만료된 세션 자동 정리 백그라운드 워커
/// </summary>
public sealed class SessionCleanupWorker : BackgroundService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionCleanupWorker> _logger;
    private readonly TimeSpan _cleanupInterval;

    public SessionCleanupWorker(
        ISessionManager sessionManager,
        ILogger<SessionCleanupWorker> logger,
        TimeSpan? cleanupInterval = null)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup worker started (interval: {Interval})", _cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                _logger.LogDebug("Running session cleanup...");

                await _sessionManager.CleanupExpiredSessionsAsync(stoppingToken);

                var sessions = await _sessionManager.ListSessionsAsync(stoppingToken);
                _logger.LogDebug("Active sessions: {Count}", sessions.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        _logger.LogInformation("Session cleanup worker stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping session cleanup worker and closing all sessions...");

        var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);
        foreach (var session in sessions)
        {
            try
            {
                await _sessionManager.CloseSessionAsync(session.SessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing session {SessionId}", session.SessionId);
            }
        }

        await base.StopAsync(cancellationToken);
    }
}

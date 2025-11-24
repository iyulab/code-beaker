using CodeBeaker.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Core.Services;

/// <summary>
/// Graceful Shutdown 서비스
/// API 서버 종료 시 활성 세션을 안전하게 정리
/// </summary>
public sealed class GracefulShutdownService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly TimeSpan _shutdownTimeout;

    public GracefulShutdownService(
        IHostApplicationLifetime appLifetime,
        ISessionManager sessionManager,
        ILogger<GracefulShutdownService> logger,
        TimeSpan? shutdownTimeout = null)
    {
        _appLifetime = appLifetime;
        _sessionManager = sessionManager;
        _logger = logger;
        _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(30);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ApplicationStopping 이벤트 등록
        _appLifetime.ApplicationStopping.Register(OnShutdown);

        _logger.LogInformation(
            "Graceful shutdown service started (timeout: {Timeout}s)",
            _shutdownTimeout.TotalSeconds);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // StopAsync는 이미 OnShutdown이 실행된 후 호출됨
        _logger.LogInformation("Graceful shutdown service stopped");
        return Task.CompletedTask;
    }

    private void OnShutdown()
    {
        _logger.LogWarning("Application shutdown initiated - cleaning up active sessions");

        try
        {
            // 비동기 작업을 동기적으로 실행 (shutdown context)
            CleanupSessionsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during graceful shutdown");
        }
    }

    private async Task CleanupSessionsAsync()
    {
        using var cts = new CancellationTokenSource(_shutdownTimeout);
        var cancellationToken = cts.Token;

        try
        {
            // 활성 세션 목록 조회
            var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                _logger.LogInformation("No active sessions to clean up");
                return;
            }

            _logger.LogInformation(
                "Cleaning up {Count} active session(s)",
                sessions.Count);

            // 모든 세션 병렬 정리
            var cleanupTasks = sessions.Select(session =>
                CleanupSessionAsync(session.SessionId, cancellationToken));

            await Task.WhenAll(cleanupTasks);

            _logger.LogInformation("All sessions cleaned up successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Session cleanup timed out after {Timeout}s - forcing shutdown",
                _shutdownTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during session cleanup");
        }
    }

    private async Task CleanupSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Cleaning up session {SessionId}", sessionId);

            var session = await _sessionManager.GetSessionAsync(sessionId, cancellationToken);

            if (session?.Environment != null)
            {
                // 환경 정리
                await session.Environment.CleanupAsync(cancellationToken);
            }

            _logger.LogDebug("Session {SessionId} cleaned up", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cleaning up session {SessionId}",
                sessionId);
        }
    }
}

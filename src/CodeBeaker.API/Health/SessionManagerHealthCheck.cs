using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeBeaker.API.Health;

/// <summary>
/// SessionManager 상태 확인을 위한 Health Check
/// </summary>
public class SessionManagerHealthCheck : IHealthCheck
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionManagerHealthCheck> _logger;

    public SessionManagerHealthCheck(
        ISessionManager sessionManager,
        ILogger<SessionManagerHealthCheck> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);
            var activeSessions = sessions.Count(s => s.State == SessionState.Active);
            var idleSessions = sessions.Count(s => s.State == SessionState.Idle);

            var data = new Dictionary<string, object>
            {
                ["TotalSessions"] = sessions.Count,
                ["ActiveSessions"] = activeSessions,
                ["IdleSessions"] = idleSessions
            };

            // SessionManager는 항상 작동해야 함
            return HealthCheckResult.Healthy(
                $"SessionManager operational ({activeSessions} active sessions)",
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SessionManager health check failed");
            return HealthCheckResult.Unhealthy(
                "SessionManager not operational",
                ex);
        }
    }
}

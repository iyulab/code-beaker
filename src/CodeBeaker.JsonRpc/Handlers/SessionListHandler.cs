using CodeBeaker.Core.Interfaces;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.JsonRpc.Handlers;

/// <summary>
/// session.list JSON-RPC 메서드 핸들러
/// </summary>
public sealed class SessionListHandler : IJsonRpcHandler
{
    private readonly ISessionManager _sessionManager;

    public string Method => "session.list";

    public SessionListHandler(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken)
    {
        var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

        return new
        {
            count = sessions.Count,
            sessions = sessions.Select(s => new
            {
                sessionId = s.SessionId,
                containerId = s.ContainerId,
                language = s.Language,
                createdAt = s.CreatedAt,
                lastActivity = s.LastActivity,
                state = s.State.ToString(),
                executionCount = s.ExecutionCount,
                idleMinutes = (DateTime.UtcNow - s.LastActivity).TotalMinutes,
                lifetimeMinutes = (DateTime.UtcNow - s.CreatedAt).TotalMinutes
            }).ToList()
        };
    }
}

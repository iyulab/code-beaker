using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.JsonRpc.Handlers;

/// <summary>
/// session.close JSON-RPC 메서드 핸들러
/// </summary>
public sealed class SessionCloseHandler : IJsonRpcHandler
{
    private readonly ISessionManager _sessionManager;

    public string Method => "session.close";

    public SessionCloseHandler(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken)
    {
        if (@params == null)
        {
            throw new ArgumentNullException(nameof(@params), "params required");
        }

        var json = JsonSerializer.Serialize(@params);
        var request = JsonSerializer.Deserialize<SessionCloseRequest>(json);

        if (request == null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new ArgumentException("Invalid request: sessionId required");
        }

        await _sessionManager.CloseSessionAsync(request.SessionId, cancellationToken);

        return new
        {
            sessionId = request.SessionId,
            closed = true
        };
    }

    private sealed class SessionCloseRequest
    {
        public string SessionId { get; set; } = string.Empty;
    }
}

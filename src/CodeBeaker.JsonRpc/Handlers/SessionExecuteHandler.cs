using System.Text.Json;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.JsonRpc.Handlers;

/// <summary>
/// session.execute JSON-RPC 메서드 핸들러
/// </summary>
public sealed class SessionExecuteHandler : IJsonRpcHandler
{
    private readonly ISessionManager _sessionManager;

    public string Method => "session.execute";

    public SessionExecuteHandler(ISessionManager sessionManager)
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
        var request = JsonSerializer.Deserialize<SessionExecuteRequest>(json);

        if (request == null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new ArgumentException("Invalid request: sessionId required");
        }

        if (request.Command == null)
        {
            throw new ArgumentException("Invalid request: command required");
        }

        var result = await _sessionManager.ExecuteInSessionAsync(
            request.SessionId,
            request.Command,
            cancellationToken);

        return new
        {
            success = result.Success,
            result = result.Result,
            error = result.Error,
            durationMs = result.DurationMs
        };
    }

    private sealed class SessionExecuteRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public Command? Command { get; set; }
    }
}

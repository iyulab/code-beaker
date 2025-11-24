using System.Text.Json;
using System.Text.Json.Serialization;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.JsonRpc.Handlers;

/// <summary>
/// session.create JSON-RPC 메서드 핸들러
/// </summary>
public sealed class SessionCreateHandler : IJsonRpcHandler
{
    private readonly ISessionManager _sessionManager;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public string Method => "session.create";

    public SessionCreateHandler(ISessionManager sessionManager)
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
        var config = JsonSerializer.Deserialize<SessionConfig>(json, _jsonOptions);

        if (config == null || string.IsNullOrWhiteSpace(config.Language))
        {
            throw new ArgumentException("Invalid session config");
        }

        var session = await _sessionManager.CreateSessionAsync(config, cancellationToken);

        return new
        {
            sessionId = session.SessionId,
            containerId = session.ContainerId,
            language = session.Language,
            createdAt = session.CreatedAt,
            state = session.State.ToString(),
            config = new
            {
                idleTimeoutMinutes = session.Config.IdleTimeoutMinutes,
                maxLifetimeMinutes = session.Config.MaxLifetimeMinutes
            }
        };
    }
}

using CodeBeaker.JsonRpc.Models;

namespace CodeBeaker.JsonRpc.Interfaces;

/// <summary>
/// JSON-RPC transport abstraction (HTTP, WebSocket, etc.)
/// </summary>
public interface IJsonRpcTransport
{
    /// <summary>
    /// Send JSON-RPC response
    /// </summary>
    Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send JSON-RPC notification (no response expected)
    /// </summary>
    Task SendNotificationAsync(string method, object? @params, CancellationToken cancellationToken = default);
}

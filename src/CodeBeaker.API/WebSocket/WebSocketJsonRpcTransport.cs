using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.JsonRpc.Models;

namespace CodeBeaker.API.WebSocket;

/// <summary>
/// JSON-RPC transport over WebSocket
/// </summary>
public sealed class WebSocketJsonRpcTransport : IJsonRpcTransport
{
    private readonly System.Net.WebSockets.WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketJsonRpcTransport(System.Net.WebSockets.WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    /// <summary>
    /// Send JSON-RPC response
    /// </summary>
    public async Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(response);
        await SendMessageAsync(json, cancellationToken);
    }

    /// <summary>
    /// Send JSON-RPC notification
    /// </summary>
    public async Task SendNotificationAsync(string method, object? @params, CancellationToken cancellationToken = default)
    {
        var notification = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Method = method,
            Params = @params,
            Id = null // Notification has no ID
        };

        var json = JsonSerializer.Serialize(notification);
        await SendMessageAsync(json, cancellationToken);
    }

    /// <summary>
    /// Send raw text message over WebSocket
    /// </summary>
    private async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n"); // Newline-delimited JSON
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken
            );
        }
        finally
        {
            _sendLock.Release();
        }
    }
}

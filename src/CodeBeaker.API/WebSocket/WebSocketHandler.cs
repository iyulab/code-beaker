using System.Net.WebSockets;
using System.Text;
using CodeBeaker.JsonRpc;

namespace CodeBeaker.API.WebSocket;

/// <summary>
/// WebSocket connection handler for JSON-RPC
/// </summary>
public sealed class WebSocketHandler
{
    private readonly JsonRpcRouter _router;
    private readonly ILogger<WebSocketHandler> _logger;

    public WebSocketHandler(JsonRpcRouter router, ILogger<WebSocketHandler> logger)
    {
        _router = router;
        _logger = logger;
    }

    /// <summary>
    /// Handle WebSocket connection
    /// </summary>
    public async Task HandleConnectionAsync(
        System.Net.WebSockets.WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("WebSocket connection opened: {ConnectionId}", connectionId);

        var transport = new WebSocketJsonRpcTransport(webSocket);
        var buffer = new byte[1024 * 4]; // 4KB buffer
        var messageBuilder = new StringBuilder();

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        cancellationToken
                    );
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);

                    // Process complete messages (newline-delimited)
                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Clear();

                        // Process each line as separate JSON-RPC message
                        var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            await ProcessMessageAsync(line, transport, connectionId, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error on {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket connection cancelled: {ConnectionId}", connectionId);
        }
        finally
        {
            _logger.LogInformation("WebSocket connection closed: {ConnectionId}", connectionId);
        }
    }

    private async Task ProcessMessageAsync(
        string json,
        WebSocketJsonRpcTransport transport,
        string connectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing message on {ConnectionId}: {Json}", connectionId, json);

            var responseJson = await _router.ProcessJsonAsync(json, cancellationToken);

            // Send response if not a notification
            if (responseJson != null)
            {
                var response = System.Text.Json.JsonSerializer.Deserialize<CodeBeaker.JsonRpc.Models.JsonRpcResponse>(responseJson);
                if (response != null)
                {
                    await transport.SendResponseAsync(response, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message on {ConnectionId}", connectionId);
        }
    }
}

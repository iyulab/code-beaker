using System.Text;
using System.Text.Json;
using CodeBeaker.AI.Agent.Models;
using Websocket.Client;

namespace CodeBeaker.AI.Agent.Services;

/// <summary>
/// CodeBeaker WebSocket JSON-RPC Client
/// </summary>
public class CodeBeakerClient : IDisposable
{
    private readonly WebsocketClient _client;
    private readonly Dictionary<int, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private int _requestId = 0;
    private readonly object _lock = new();

    public CodeBeakerClient(string websocketUrl = "ws://localhost:5039/ws/jsonrpc")
    {
        _client = new WebsocketClient(new Uri(websocketUrl));
        _client.ReconnectTimeout = TimeSpan.FromSeconds(30);
        _client.MessageReceived.Subscribe(HandleMessage);
    }

    public async Task ConnectAsync()
    {
        await _client.Start();
        Console.WriteLine("[CodeBeaker] Connected to CodeBeaker API");
    }

    private void HandleMessage(ResponseMessage message)
    {
        try
        {
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(message.Text);
            if (response != null && _pendingRequests.TryGetValue(response.Id, out var tcs))
            {
                tcs.SetResult(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CodeBeaker] Error handling message: {ex.Message}");
        }
    }

    public async Task<T?> SendRequestAsync<T>(string method, object? parameters = null, CancellationToken ct = default)
    {
        int requestId;
        lock (_lock)
        {
            requestId = ++_requestId;
        }

        var request = new JsonRpcRequest
        {
            Id = requestId,
            Method = method,
            Params = parameters
        };

        var tcs = new TaskCompletionSource<JsonRpcResponse>();
        _pendingRequests[requestId] = tcs;

        try
        {
            var json = JsonSerializer.Serialize(request);
            _client.Send(json);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var response = await tcs.Task.WaitAsync(linkedCts.Token);

            if (response.Error != null)
            {
                throw new Exception($"JSON-RPC Error: {response.Error.Message}");
            }

            if (response.Result == null)
            {
                return default;
            }

            // Deserialize result
            var resultJson = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<T>(resultJson);
        }
        finally
        {
            _pendingRequests.Remove(requestId);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

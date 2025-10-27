using System.Collections.Concurrent;
using System.Text.Json;
using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.JsonRpc.Models;

namespace CodeBeaker.JsonRpc;

/// <summary>
/// JSON-RPC method router and dispatcher
/// </summary>
public sealed class JsonRpcRouter
{
    private readonly ConcurrentDictionary<string, IJsonRpcHandler> _handlers = new();

    /// <summary>
    /// Register a method handler
    /// </summary>
    public void RegisterHandler(IJsonRpcHandler handler)
    {
        _handlers[handler.Method] = handler;
    }

    /// <summary>
    /// Register multiple handlers
    /// </summary>
    public void RegisterHandlers(IEnumerable<IJsonRpcHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            RegisterHandler(handler);
        }
    }

    /// <summary>
    /// Process JSON-RPC request and return response
    /// </summary>
    public async Task<JsonRpcResponse?> ProcessRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Method))
        {
            return JsonRpcResponse.Failure(
                request.Id,
                JsonRpcError.InvalidRequest("Method is required")
            );
        }

        if (request.JsonRpc != "2.0")
        {
            return JsonRpcResponse.Failure(
                request.Id,
                JsonRpcError.InvalidRequest("jsonrpc must be '2.0'")
            );
        }

        // Notifications don't get responses
        if (request.IsNotification)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteMethodAsync(request.Method, request.Params, cancellationToken);
                }
                catch
                {
                    // Notifications don't report errors
                }
            }, cancellationToken);

            return null;
        }

        // Execute method and return response
        try
        {
            var result = await ExecuteMethodAsync(request.Method, request.Params, cancellationToken);
            return JsonRpcResponse.Success(request.Id, result);
        }
        catch (JsonRpcException ex)
        {
            return JsonRpcResponse.Failure(request.Id, ex.Error);
        }
        catch (Exception ex)
        {
            return JsonRpcResponse.Failure(
                request.Id,
                JsonRpcError.InternalError(ex.Message)
            );
        }
    }

    /// <summary>
    /// Process JSON string and return JSON response
    /// </summary>
    public async Task<string?> ProcessJsonAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json);
            if (request == null)
            {
                var errorResponse = JsonRpcResponse.Failure(
                    null,
                    JsonRpcError.ParseError("Invalid JSON")
                );
                return JsonSerializer.Serialize(errorResponse);
            }

            var response = await ProcessRequestAsync(request, cancellationToken);
            return response == null ? null : JsonSerializer.Serialize(response);
        }
        catch (JsonException ex)
        {
            var errorResponse = JsonRpcResponse.Failure(
                null,
                JsonRpcError.ParseError(ex.Message)
            );
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task<object?> ExecuteMethodAsync(
        string method,
        object? @params,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(method, out var handler))
        {
            throw new JsonRpcException(JsonRpcError.MethodNotFound(method));
        }

        try
        {
            return await handler.HandleAsync(@params, cancellationToken);
        }
        catch (JsonRpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonRpcException(JsonRpcError.InternalError(ex.Message));
        }
    }
}

/// <summary>
/// JSON-RPC exception (carries error object)
/// </summary>
public sealed class JsonRpcException : Exception
{
    public JsonRpcError Error { get; }

    public JsonRpcException(JsonRpcError error) : base(error.Message)
    {
        Error = error;
    }
}

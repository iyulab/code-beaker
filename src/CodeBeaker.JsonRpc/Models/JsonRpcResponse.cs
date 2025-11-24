using System.Text.Json.Serialization;

namespace CodeBeaker.JsonRpc.Models;

/// <summary>
/// JSON-RPC 2.0 Response (success)
/// </summary>
public sealed class JsonRpcResponse
{
    /// <summary>
    /// JSON-RPC protocol version (must be "2.0")
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Request identifier (must match request.id)
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// Successful result (mutually exclusive with Error)
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    /// <summary>
    /// Error details (mutually exclusive with Result)
    /// </summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }

    /// <summary>
    /// Create success response
    /// </summary>
    public static JsonRpcResponse Success(object? id, object? result) => new()
    {
        Id = id,
        Result = result
    };

    /// <summary>
    /// Create error response
    /// </summary>
    public static JsonRpcResponse Failure(object? id, JsonRpcError error) => new()
    {
        Id = id,
        Error = error
    };
}

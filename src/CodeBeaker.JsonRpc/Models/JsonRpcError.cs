using System.Text.Json.Serialization;

namespace CodeBeaker.JsonRpc.Models;

/// <summary>
/// JSON-RPC 2.0 Error object
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>
    /// Error code (integer)
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error data (optional)
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    // Standard JSON-RPC 2.0 error codes
    public static JsonRpcError ParseError(string? details = null) => new()
    {
        Code = -32700,
        Message = "Parse error",
        Data = details
    };

    public static JsonRpcError InvalidRequest(string? details = null) => new()
    {
        Code = -32600,
        Message = "Invalid Request",
        Data = details
    };

    public static JsonRpcError MethodNotFound(string method) => new()
    {
        Code = -32601,
        Message = "Method not found",
        Data = method
    };

    public static JsonRpcError InvalidParams(string? details = null) => new()
    {
        Code = -32602,
        Message = "Invalid params",
        Data = details
    };

    public static JsonRpcError InternalError(string? details = null) => new()
    {
        Code = -32603,
        Message = "Internal error",
        Data = details
    };

    // Server errors (-32000 to -32099)
    public static JsonRpcError ServerError(int code, string message, object? data = null) => new()
    {
        Code = code,
        Message = message,
        Data = data
    };
}

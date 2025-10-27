using System.Text.Json.Serialization;

namespace CodeBeaker.JsonRpc.Models;

/// <summary>
/// JSON-RPC 2.0 Request
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>
    /// JSON-RPC protocol version (must be "2.0")
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Request identifier (string, number, or null for notifications)
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// Method name to invoke
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Method parameters (object or array)
    /// </summary>
    [JsonPropertyName("params")]
    public object? Params { get; set; }

    /// <summary>
    /// Check if this is a notification (no response expected)
    /// </summary>
    [JsonIgnore]
    public bool IsNotification => Id == null;
}

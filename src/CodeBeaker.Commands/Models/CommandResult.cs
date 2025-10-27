using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Result of command execution
/// </summary>
public sealed class CommandResult
{
    /// <summary>
    /// Command ID (matches Command.Id)
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Success status
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Result data (command-specific)
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    /// <summary>
    /// Create success result
    /// </summary>
    public static CommandResult Ok(object? result = null, int durationMs = 0) => new()
    {
        Success = true,
        Result = result,
        DurationMs = durationMs
    };

    /// <summary>
    /// Create error result
    /// </summary>
    public static CommandResult Fail(string error, int durationMs = 0) => new()
    {
        Success = false,
        Error = error,
        DurationMs = durationMs
    };
}

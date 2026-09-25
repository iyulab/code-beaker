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
    /// Create the result of a shell command that ran to completion.
    /// </summary>
    /// <remarks>
    /// <see cref="Success"/> is <see langword="true"/> exactly when the exit code is zero, and
    /// <see cref="Result"/> is <paramref name="output"/> either way. <see cref="Error"/> is
    /// <see langword="null"/> on success; on a non-zero exit it is the command's standard error,
    /// or a message naming the exit code when the command wrote nothing there.
    /// </remarks>
    public static CommandResult FromShellOutput(ShellCommandOutput output, int durationMs = 0)
    {
        ArgumentNullException.ThrowIfNull(output);

        var succeeded = output.ExitCode == 0;
        return new CommandResult
        {
            Success = succeeded,
            Result = output,
            Error = succeeded
                ? null
                : string.IsNullOrWhiteSpace(output.Stderr)
                    ? $"Command failed with exit code {output.ExitCode}"
                    : output.Stderr.TrimEnd(),
            DurationMs = durationMs
        };
    }

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

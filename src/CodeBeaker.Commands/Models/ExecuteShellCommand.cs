using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to execute shell command directly (optimized path)
/// </summary>
public sealed class ExecuteShellCommand : Command
{
    public override string Type => "shell";

    /// <summary>
    /// Command to execute (e.g., "dotnet", "python")
    /// </summary>
    [JsonPropertyName("command")]
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Command arguments
    /// </summary>
    [JsonPropertyName("args")]
    public List<string> Args { get; set; } = new();

    /// <summary>
    /// Working directory
    /// </summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Environment variables
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string>? Environment { get; set; }
}

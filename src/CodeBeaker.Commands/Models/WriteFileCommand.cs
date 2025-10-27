using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to write file content
/// </summary>
public sealed class WriteFileCommand : Command
{
    public override string Type => "write_file";

    /// <summary>
    /// File path (absolute or relative to workspace)
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// File mode (Create, Append, etc.)
    /// </summary>
    [JsonPropertyName("mode")]
    public FileWriteMode Mode { get; set; } = FileWriteMode.Create;
}

/// <summary>
/// File write modes
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileWriteMode
{
    Create,
    Append,
    Overwrite
}

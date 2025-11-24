using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to read file content
/// </summary>
public sealed class ReadFileCommand : Command
{
    public override string Type => "read_file";

    /// <summary>
    /// File path to read
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Optional encoding (default: UTF-8)
    /// </summary>
    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }
}

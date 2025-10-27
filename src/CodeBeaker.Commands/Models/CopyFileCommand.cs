using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to copy file
/// </summary>
public sealed class CopyFileCommand : Command
{
    public override string Type => "copy_file";

    /// <summary>
    /// Source file path
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Destination file path
    /// </summary>
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Overwrite if destination exists
    /// </summary>
    [JsonPropertyName("overwrite")]
    public bool Overwrite { get; set; } = true;
}

using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to create directory
/// </summary>
public sealed class CreateDirectoryCommand : Command
{
    public override string Type => "create_dir";

    /// <summary>
    /// Directory path to create
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Create parent directories if they don't exist
    /// </summary>
    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = true;
}

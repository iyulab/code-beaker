using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to list files and directories in a path
/// Phase 12: AI Agent Integration
/// </summary>
public sealed class ListFilesCommand : Command
{
    public override string Type => "list_files";

    /// <summary>
    /// Directory path to list (relative to workspace, default: ".")
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = ".";

    /// <summary>
    /// Recursive traversal
    /// </summary>
    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// File pattern filter (glob pattern, e.g., "*.cs", "*.py")
    /// </summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>
    /// Include hidden files (starting with .)
    /// </summary>
    [JsonPropertyName("include_hidden")]
    public bool IncludeHidden { get; set; } = false;

    /// <summary>
    /// Maximum depth (0 = unlimited)
    /// </summary>
    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; } = 0;
}

/// <summary>
/// File system entry type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileEntryType
{
    File,
    Directory
}

/// <summary>
/// File tree node for list_files result
/// </summary>
public class FileTreeNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public FileEntryType Type { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }

    [JsonPropertyName("children")]
    public List<FileTreeNode>? Children { get; set; }
}

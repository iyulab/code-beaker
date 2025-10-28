using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to generate unified diff between two files or content
/// Phase 12: AI Agent Integration
/// </summary>
public sealed class DiffCommand : Command
{
    public override string Type => "diff";

    /// <summary>
    /// Original file path (optional if OriginalContent provided)
    /// </summary>
    [JsonPropertyName("original_path")]
    public string? OriginalPath { get; set; }

    /// <summary>
    /// Modified file path (optional if ModifiedContent provided)
    /// </summary>
    [JsonPropertyName("modified_path")]
    public string? ModifiedPath { get; set; }

    /// <summary>
    /// Original content (optional if OriginalPath provided)
    /// </summary>
    [JsonPropertyName("original_content")]
    public string? OriginalContent { get; set; }

    /// <summary>
    /// Modified content (optional if ModifiedPath provided)
    /// </summary>
    [JsonPropertyName("modified_content")]
    public string? ModifiedContent { get; set; }

    /// <summary>
    /// Diff format
    /// </summary>
    [JsonPropertyName("format")]
    public DiffFormat Format { get; set; } = DiffFormat.Unified;

    /// <summary>
    /// Context lines (default: 3)
    /// </summary>
    [JsonPropertyName("context_lines")]
    public int ContextLines { get; set; } = 3;
}

/// <summary>
/// Diff format type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiffFormat
{
    Unified,
    Context
}

/// <summary>
/// Result of diff operation
/// </summary>
public class DiffResult
{
    /// <summary>
    /// Unified diff text
    /// </summary>
    [JsonPropertyName("diff")]
    public string Diff { get; set; } = string.Empty;

    /// <summary>
    /// Number of lines added
    /// </summary>
    [JsonPropertyName("added_lines")]
    public int AddedLines { get; set; }

    /// <summary>
    /// Number of lines removed
    /// </summary>
    [JsonPropertyName("removed_lines")]
    public int RemovedLines { get; set; }

    /// <summary>
    /// Number of lines modified (contextual)
    /// </summary>
    [JsonPropertyName("modified_lines")]
    public int ModifiedLines { get; set; }

    /// <summary>
    /// Whether files are identical
    /// </summary>
    [JsonPropertyName("identical")]
    public bool Identical { get; set; }
}

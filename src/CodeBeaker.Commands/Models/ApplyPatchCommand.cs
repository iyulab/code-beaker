using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to apply unified diff patch to files
/// Phase 13: Debug & Improvement
/// </summary>
public sealed class ApplyPatchCommand : Command
{
    public override string Type => "apply_patch";

    /// <summary>
    /// Unified diff text to apply
    /// </summary>
    [JsonPropertyName("patch")]
    public string Patch { get; set; } = string.Empty;

    /// <summary>
    /// Target directory (default: workspace root)
    /// </summary>
    [JsonPropertyName("target_path")]
    public string? TargetPath { get; set; }

    /// <summary>
    /// Dry run mode (validate without applying)
    /// </summary>
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Strip path components (like patch -p)
    /// </summary>
    [JsonPropertyName("strip")]
    public int Strip { get; set; } = 0;
}

/// <summary>
/// Result of patch application
/// </summary>
public class PatchResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("files_patched")]
    public int FilesPatched { get; set; }

    [JsonPropertyName("hunks_applied")]
    public int HunksApplied { get; set; }

    [JsonPropertyName("hunks_failed")]
    public int HunksFailed { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }

    [JsonPropertyName("modified_content")]
    public string? ModifiedContent { get; set; }
}

using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to install packages/dependencies
/// Phase 10: Advanced Features - Package Management
/// </summary>
public sealed class InstallPackagesCommand : Command
{
    public override string Type => "install_packages";

    /// <summary>
    /// Package names to install
    /// </summary>
    [JsonPropertyName("packages")]
    public List<string> Packages { get; set; } = new();

    /// <summary>
    /// Optional requirements file path (requirements.txt, package.json, etc.)
    /// </summary>
    [JsonPropertyName("requirements_file")]
    public string? RequirementsFile { get; set; }

    /// <summary>
    /// Use global installation (default: false, installs locally)
    /// </summary>
    [JsonPropertyName("global")]
    public bool Global { get; set; } = false;

    /// <summary>
    /// Additional package manager flags
    /// </summary>
    [JsonPropertyName("flags")]
    public List<string> Flags { get; set; } = new();
}

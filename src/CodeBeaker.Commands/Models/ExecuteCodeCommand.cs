using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Command to execute code in a specific language
/// </summary>
public sealed class ExecuteCodeCommand : Command
{
    public override string Type => "execute";

    /// <summary>
    /// Programming language
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Source code to execute
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Package dependencies
    /// </summary>
    [JsonPropertyName("packages")]
    public List<string>? Packages { get; set; }

    /// <summary>
    /// Execution timeout in seconds
    /// </summary>
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Memory limit in MB
    /// </summary>
    [JsonPropertyName("memoryLimit")]
    public int MemoryLimit { get; set; } = 512;

    /// <summary>
    /// CPU limit (cores)
    /// </summary>
    [JsonPropertyName("cpuLimit")]
    public double CpuLimit { get; set; } = 1.0;
}

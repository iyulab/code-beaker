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
    /// File content, encoded as <see cref="Encoding"/> says
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// How <see cref="Content"/> is encoded: text written as UTF-8, or base64 for arbitrary bytes
    /// </summary>
    [JsonPropertyName("encoding")]
    public FileContentEncoding Encoding { get; set; } = FileContentEncoding.Utf8;

    /// <summary>
    /// File mode (Create, Append, etc.)
    /// </summary>
    [JsonPropertyName("mode")]
    public FileWriteMode Mode { get; set; } = FileWriteMode.Create;

    /// <summary>
    /// The bytes to write: <see cref="Content"/> decoded according to <see cref="Encoding"/>
    /// </summary>
    /// <exception cref="FormatException"><see cref="Encoding"/> is base64 and <see cref="Content"/> is not valid base64</exception>
    public byte[] GetContentBytes() => Encoding switch
    {
        FileContentEncoding.Base64 => Convert.FromBase64String(Content),
        _ => System.Text.Encoding.UTF8.GetBytes(Content)
    };
}

/// <summary>
/// Encodings of <see cref="WriteFileCommand.Content"/>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileContentEncoding
{
    Utf8,
    Base64
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

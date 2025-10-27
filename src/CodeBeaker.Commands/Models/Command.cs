using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// Abstract base class for all commands
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ExecuteCodeCommand), typeDiscriminator: "execute")]
[JsonDerivedType(typeof(WriteFileCommand), typeDiscriminator: "write_file")]
[JsonDerivedType(typeof(ReadFileCommand), typeDiscriminator: "read_file")]
[JsonDerivedType(typeof(CreateDirectoryCommand), typeDiscriminator: "create_dir")]
[JsonDerivedType(typeof(CopyFileCommand), typeDiscriminator: "copy_file")]
[JsonDerivedType(typeof(ExecuteShellCommand), typeDiscriminator: "shell")]
public abstract class Command
{
    /// <summary>
    /// Command type discriminator
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    /// <summary>
    /// Optional command identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

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
[JsonDerivedType(typeof(InstallPackagesCommand), typeDiscriminator: "install_packages")]
[JsonDerivedType(typeof(ListFilesCommand), typeDiscriminator: "list_files")]
[JsonDerivedType(typeof(DiffCommand), typeDiscriminator: "diff")]
[JsonDerivedType(typeof(ApplyPatchCommand), typeDiscriminator: "apply_patch")]
public abstract class Command
{
    /// <summary>
    /// Command type discriminator. The JSON "type" property is written and read
    /// by the polymorphic metadata above; declaring it here as well would make
    /// System.Text.Json reject the model, so the property is kept out of the
    /// serialized shape.
    /// </summary>
    [JsonIgnore]
    public abstract string Type { get; }

    /// <summary>
    /// Optional command identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

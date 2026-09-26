using System.Text.Json;
using CodeBeaker.Commands.Models;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Commands.Tests;

/// <summary>
/// The JSON shape of a <see cref="Command"/> is part of the JSON-RPC contract:
/// "type" is the polymorphic discriminator and must round-trip through
/// System.Text.Json without the model declaring a second "type" property.
/// </summary>
public sealed class CommandSerializationTests
{
    [Fact]
    public void Deserialize_WithTypeDiscriminator_ReturnsDerivedCommand()
    {
        const string json = """{"type":"write_file","path":"/workspace/a.txt","content":"hi","mode":"Create"}""";

        var command = JsonSerializer.Deserialize<Command>(json);

        command.Should().BeOfType<WriteFileCommand>();
        command!.Type.Should().Be("write_file");
    }

    [Fact]
    public void WriteFile_WithoutEncoding_IsUtf8Text()
    {
        const string json = """{"type":"write_file","path":"a.txt","content":"héllo"}""";

        var command = (WriteFileCommand)JsonSerializer.Deserialize<Command>(json)!;

        command.Encoding.Should().Be(FileContentEncoding.Utf8);
        command.GetContentBytes().Should().Equal(System.Text.Encoding.UTF8.GetBytes("héllo"));
    }

    [Fact]
    public void WriteFile_WithBase64Encoding_DecodesToTheExactBytes()
    {
        const string json = """{"type":"write_file","path":"a.bin","content":"AAH/","encoding":"Base64"}""";

        var command = (WriteFileCommand)JsonSerializer.Deserialize<Command>(json)!;

        command.GetContentBytes().Should().Equal(0x00, 0x01, 0xFF);
    }

    [Fact]
    public void WriteFile_Encoding_SerializesAsItsName()
    {
        Command command = new WriteFileCommand { Path = "a.bin", Content = "AA==", Encoding = FileContentEncoding.Base64 };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(command));

        document.RootElement.GetProperty("encoding").GetString().Should().Be("Base64");
    }

    [Fact]
    public void WriteFile_Base64ThatIsNotBase64_IsRejected()
    {
        var command = new WriteFileCommand { Path = "a.bin", Content = "not base64!", Encoding = FileContentEncoding.Base64 };

        command.Invoking(c => c.GetContentBytes()).Should().Throw<FormatException>();
    }

    [Fact]
    public void Serialize_AsBaseType_WritesTypeDiscriminatorOnce()
    {
        Command command = new ReadFileCommand { Path = "/workspace/a.txt" };

        var json = JsonSerializer.Serialize(command);
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("type").GetString().Should().Be("read_file");
        document.RootElement.EnumerateObject().Count(p => p.Name == "type").Should().Be(1);
    }

    [Theory]
    [InlineData("execute", typeof(ExecuteCodeCommand))]
    [InlineData("shell", typeof(ExecuteShellCommand))]
    [InlineData("read_file", typeof(ReadFileCommand))]
    [InlineData("list_files", typeof(ListFilesCommand))]
    public void Deserialize_EveryDiscriminator_MapsToItsCommandType(string discriminator, Type expected)
    {
        var json = $$"""{"type":"{{discriminator}}"}""";

        var command = JsonSerializer.Deserialize<Command>(json);

        command.Should().BeOfType(expected);
    }
}

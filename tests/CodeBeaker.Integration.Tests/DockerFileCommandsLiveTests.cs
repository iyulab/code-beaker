using System.Security.Cryptography;
using System.Text;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Integration.Tests.TestHelpers;
using CodeBeaker.Runtimes;
using CodeBeaker.Runtimes.Docker;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// The Docker runtime's file commands against a real container. For <see cref="WriteFileCommand"/>:
/// what lands in the file is exactly what was sent, whatever its size, and the command reports
/// success only once the file is complete. For every command: a failure is reported as one. A mock of the Docker client cannot show this — the content crosses a real exec
/// stdin stream, and the failure modes (a writer blocked on output nobody reads, a result
/// returned before the writer has finished) only exist on the real stream.
/// </summary>
public sealed class DockerFileCommandsLiveTests
{
    public static TheoryData<string> Languages() => [.. LanguageCatalog.Names];

    /// <summary>
    /// Every sandbox image hands its working directory to the non-root user it runs as. The
    /// Python and Node.js images once created <c>/workspace</c> as root and then switched user,
    /// so nothing could be written there.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Languages))]
    public async Task WriteFile_IntoTheWorkingDirectory_WorksInEverySandboxImage(string language)
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);

        await using var environment = await new DockerRuntime().CreateEnvironmentAsync(new RuntimeConfig { Environment = language });

        var write = await environment.ExecuteAsync(new WriteFileCommand { Path = "probe.txt", Content = language });

        Assert.True(write.Success, write.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes(language)), await Sha256InContainerAsync(environment, "probe.txt"));
    }

    [SkippableFact]
    public async Task WriteFile_LargerThanAPipeBuffer_LandsIntact()
    {
        await using var environment = await CreateEnvironmentAsync();

        // Several times a 64 KiB pipe buffer, so a writer echoing to an undrained stream stalls.
        var content = string.Concat(Enumerable.Repeat("0123456789abcdef", 64 * 1024));

        var write = await WithTimeout(environment.ExecuteAsync(
            new WriteFileCommand { Path = "large.txt", Content = content }));

        Assert.True(write.Success, write.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes(content)), await Sha256InContainerAsync(environment, "large.txt"));
    }

    [SkippableFact]
    public async Task WriteFile_Append_AddsToTheExistingFile()
    {
        await using var environment = await CreateEnvironmentAsync();

        await environment.ExecuteAsync(new WriteFileCommand { Path = "log.txt", Content = "first\n" });
        var append = await environment.ExecuteAsync(
            new WriteFileCommand { Path = "log.txt", Content = "second\n", Mode = FileWriteMode.Append });

        Assert.True(append.Success, append.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes("first\nsecond\n")), await Sha256InContainerAsync(environment, "log.txt"));
    }

    [SkippableFact]
    public async Task WriteFile_Create_ReplacesAnExistingFile()
    {
        await using var environment = await CreateEnvironmentAsync();

        await environment.ExecuteAsync(new WriteFileCommand { Path = "a.txt", Content = "a much longer original\n" });
        var create = await environment.ExecuteAsync(new WriteFileCommand { Path = "a.txt", Content = "short\n" });

        Assert.True(create.Success, create.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes("short\n")), await Sha256InContainerAsync(environment, "a.txt"));
    }

    [SkippableFact]
    public async Task WriteFile_IntoAMissingDirectory_CreatesIt()
    {
        await using var environment = await CreateEnvironmentAsync();

        var write = await environment.ExecuteAsync(
            new WriteFileCommand { Path = "no/such/dir/file.txt", Content = "x" });

        Assert.True(write.Success, write.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes("x")), await Sha256InContainerAsync(environment, "no/such/dir/file.txt"));
    }

    [SkippableFact]
    public async Task WriteFile_WhereTheSandboxUserCannotWrite_FailsInsteadOfReportingSuccess()
    {
        await using var environment = await CreateEnvironmentAsync();

        var write = await environment.ExecuteAsync(new WriteFileCommand { Path = "/etc/codebeaker-test", Content = "x" });

        Assert.False(write.Success);
        Assert.Contains("denied", write.Error, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task WriteFile_Base64_LandsAsTheExactBytes()
    {
        await using var environment = await CreateEnvironmentAsync();

        // Every byte value, NUL included, and more than a pipe buffer of them.
        var bytes = new byte[512 * 1024];
        new Random(155).NextBytes(bytes);
        bytes[0] = 0;

        var write = await WithTimeout(environment.ExecuteAsync(new WriteFileCommand
        {
            Path = "blob.bin",
            Content = Convert.ToBase64String(bytes),
            Encoding = FileContentEncoding.Base64
        }));

        Assert.True(write.Success, write.Error);
        Assert.Equal(Sha256(bytes), await Sha256InContainerAsync(environment, "blob.bin"));
    }

    [SkippableFact]
    public async Task WriteFile_Base64_ThatIsNotBase64_FailsWithoutWriting()
    {
        await using var environment = await CreateEnvironmentAsync();

        var write = await environment.ExecuteAsync(new WriteFileCommand
        {
            Path = "bad.bin",
            Content = "not base64!",
            Encoding = FileContentEncoding.Base64
        });

        Assert.False(write.Success);
        var exists = await environment.ExecuteAsync(new ExecuteShellCommand { CommandName = "test", Args = ["-e", "bad.bin"] });
        Assert.False(exists.Success);
    }

    [SkippableFact]
    public async Task ReadFile_ThatDoesNotExist_FailsInsteadOfReturningEmptyContent()
    {
        await using var environment = await CreateEnvironmentAsync();

        var read = await environment.ExecuteAsync(new ReadFileCommand { Path = "missing.txt" });

        Assert.False(read.Success);
        Assert.Contains("missing.txt", read.Error);
    }

    [SkippableFact]
    public async Task WriteFile_PathStartingWithADash_IsTakenAsAPath()
    {
        await using var environment = await CreateEnvironmentAsync();

        var write = await environment.ExecuteAsync(new WriteFileCommand { Path = "--version", Content = "data" });

        Assert.True(write.Success, write.Error);
        Assert.Equal(Sha256(Encoding.UTF8.GetBytes("data")), await Sha256InContainerAsync(environment, "./--version"));
    }

    [SkippableFact]
    public async Task ListFiles_ReturnsWhatIsThere_AndFailsOnAMissingDirectory()
    {
        await using var environment = await CreateEnvironmentAsync();
        await environment.ExecuteAsync(new WriteFileCommand { Path = "tree/a.txt", Content = "a" });

        var listed = await environment.ExecuteAsync(new ListFilesCommand { Path = "tree" });
        var missing = await environment.ExecuteAsync(new ListFilesCommand { Path = "no-such-dir" });

        Assert.True(listed.Success, listed.Error);
        Assert.Contains("a.txt", System.Text.Json.JsonSerializer.Serialize(listed.Result));
        Assert.False(missing.Success);
        Assert.Contains("no-such-dir", missing.Error);
    }

    private static async Task<IExecutionEnvironment> CreateEnvironmentAsync()
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);

        return await new DockerRuntime().CreateEnvironmentAsync(new RuntimeConfig { Environment = "python" });
    }

    private static async Task<string> Sha256InContainerAsync(IExecutionEnvironment environment, string path)
    {
        var result = await environment.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "sha256sum", Args = ["--", path] });
        var output = Assert.IsType<ShellCommandOutput>(result.Result);
        Assert.True(result.Success, output.Stderr);
        return output.Stdout.Split(' ')[0];
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static async Task<CommandResult> WithTimeout(Task<CommandResult> task)
    {
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(finished == task, "The write did not complete within 30 seconds");
        return await task;
    }
}

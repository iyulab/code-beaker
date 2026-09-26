using System.Text.Json;
using CodeBeaker.Commands.Interfaces;
using CodeBeaker.Commands.Models;
using CodeBeaker.Commands.Utilities;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Moq;

namespace CodeBeaker.Commands.Tests;

/// <summary>
/// Unit tests for CommandExecutor
/// Phase 14: Runtime Handlers Testing
/// </summary>
public class CommandExecutorTests : IDisposable
{
    private readonly Mock<IDockerClient> _dockerMock;
    private readonly Mock<IContainerExecStreamIO> _streamIOMock;
    private readonly CommandExecutor _executor;
    private readonly string _testContainerId = "test-container-123";
    private readonly List<ContainerExecCreateParameters> _execParams = new();

    public CommandExecutorTests()
    {
        _dockerMock = new Mock<IDockerClient>();
        _streamIOMock = new Mock<IContainerExecStreamIO>();
        _streamIOMock
            .Setup(io => io.WriteInputAsync(It.IsAny<MultiplexedStream>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _executor = new CommandExecutor(_dockerMock.Object, _streamIOMock.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region ListFilesCommand Tests

    [Fact]
    public async Task ListFiles_ShouldReturnFileTree_WhenFilesExist()
    {
        // Arrange
        var command = new ListFilesCommand
        {
            Path = ".",
            Recursive = true,
            Pattern = "*.txt",
            IncludeHidden = false,
            MaxDepth = 2
        };

        var findOutput = @"d|4096|1698508800|.
f|1234|1698508800|./file1.txt
f|5678|1698508800|./file2.txt
d|4096|1698508800|./subdir
f|9012|1698508800|./subdir/file3.txt";

        SetupDockerExec(findOutput, "");

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert — find prints the starting point first; it is the root, not a child of it,
        // and entries hang under their parent by the container's '/' whatever the host OS.
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Result));
        var tree = document.RootElement.GetProperty("tree");
        tree.GetProperty("path").GetString().Should().Be(".");
        tree.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Equal("file1.txt", "file2.txt", "subdir");
        var subdir = tree.GetProperty("children")[2];
        subdir.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("path").GetString())
            .Should().Equal("./subdir/file3.txt");
    }

    [Fact]
    public async Task ListFiles_ShouldHandleEmptyDirectory()
    {
        // Arrange
        var command = new ListFilesCommand
        {
            Path = ".",
            Recursive = false
        };

        var findOutput = "d|4096|1698508800|.";
        SetupDockerExec(findOutput, "");

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ListFiles_ShouldFail_WhenDirectoryNotFound()
    {
        // Arrange
        var command = new ListFilesCommand
        {
            Path = "/nonexistent"
        };

        SetupDockerExec("", "find: '/nonexistent': No such file or directory", exitCode: 1);

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed to list files");
    }

    [Fact]
    public async Task ListFiles_ShouldInvokeFindDirectly_WithoutAShell()
    {
        // Arrange
        var command = new ListFilesCommand { Path = ".", Recursive = true };
        SetupDockerExec("d|4096|1698508800|.", "");

        // Act
        await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        var cmd = _execParams.Should().ContainSingle().Subject.Cmd;
        cmd[0].Should().Be("find");
        cmd.Should().NotContain("sh");
        cmd.Should().NotContain("-c");
    }

    [Fact]
    public async Task ListFiles_ShouldPassPathAsOneArgument_WhenPathContainsShellMetacharacters()
    {
        // Arrange
        const string hostilePath = "/workspace; touch /tmp/pwned";
        var command = new ListFilesCommand { Path = hostilePath, Recursive = true };
        SetupDockerExec("d|4096|1698508800|.", "");

        // Act
        await _executor.ExecuteAsync(command, _testContainerId);

        // Assert — the value survives intact as a single argv element and is spliced nowhere else
        var cmd = _execParams.Should().ContainSingle().Subject.Cmd;
        cmd.Should().Contain(hostilePath);
        cmd.Count(a => a.Contains("touch /tmp/pwned")).Should().Be(1);
    }

    [Fact]
    public async Task ListFiles_ShouldPassPatternAsOneArgument_WhenPatternContainsAQuote()
    {
        // Arrange
        const string hostilePattern = "x'; touch /tmp/pwned; echo '";
        var command = new ListFilesCommand { Path = ".", Recursive = true, Pattern = hostilePattern };
        SetupDockerExec("d|4096|1698508800|.", "");

        // Act
        await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        var cmd = _execParams.Should().ContainSingle().Subject.Cmd;
        var nameIndex = cmd.IndexOf("-name");
        nameIndex.Should().BeGreaterThan(-1);
        cmd[nameIndex + 1].Should().Be(hostilePattern);
    }

    [Fact]
    public async Task ListFiles_ShouldBuildEveryFindFlagAsItsOwnArgument()
    {
        // Arrange
        var command = new ListFilesCommand
        {
            Path = "/workspace",
            Recursive = true,
            MaxDepth = 3,
            IncludeHidden = false,
            Pattern = "*.cs"
        };
        SetupDockerExec("d|4096|1698508800|/workspace", "");

        // Act
        await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        var cmd = _execParams.Should().ContainSingle().Subject.Cmd;
        cmd.Should().Equal(
            "find", "/workspace",
            "-maxdepth", "3",
            "-not", "-path", "*/.*",
            "-name", "*.cs",
            "-printf", @"%y|%s|%T@|%p\n");
    }

    [Fact]
    public async Task ListFiles_ShouldLimitDepthToOne_WhenNotRecursive()
    {
        // Arrange
        var command = new ListFilesCommand { Path = ".", Recursive = false, IncludeHidden = true };
        SetupDockerExec("d|4096|1698508800|.", "");

        // Act
        await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        var cmd = _execParams.Should().ContainSingle().Subject.Cmd;
        cmd.Should().Equal("find", ".", "-maxdepth", "1", "-printf", @"%y|%s|%T@|%p\n");
    }

    #endregion

    #region ExecuteShellCommand Tests

    [Fact]
    public async Task Shell_WithZeroExit_Succeeds_AndCarriesTheTypedOutput()
    {
        // Arrange
        SetupDockerExec("hello\n", "", exitCode: 0);

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "echo", Args = ["hello"] }, _testContainerId);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Result.Should().Be(new ShellCommandOutput(0, "hello\n", ""));
    }

    [Fact]
    public async Task Shell_WithNonZeroExit_Fails_KeepsTheOutput_AndReportsStderrAsTheError()
    {
        // Arrange — a failed command still ran; what it printed is what explains the failure
        SetupDockerExec("partial\n", "boom\n", exitCode: 2);

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "sh", Args = ["-c", "exit 2"] }, _testContainerId);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(new ShellCommandOutput(2, "partial\n", "boom\n"));
        result.Error.Should().Be("boom");
    }

    [Fact]
    public async Task Shell_WithNonZeroExit_AndNoStderr_NamesTheExitCodeInTheError()
    {
        // Arrange
        SetupDockerExec("", "", exitCode: 3);

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "false" }, _testContainerId);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().Be(new ShellCommandOutput(3, "", ""));
        result.Error.Should().Contain("exit code 3");
    }

    [Fact]
    public async Task Shell_WritingToBothStreams_WithZeroExit_KeepsStderrInTheOutput_NotTheError()
    {
        // Arrange — stderr on a successful command is diagnostics (warnings, progress), not failure
        SetupDockerExec("out\n", "warn\n", exitCode: 0);

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "sh", Args = ["-c", "echo out; echo warn >&2"] },
            _testContainerId);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Result.Should().Be(new ShellCommandOutput(0, "out\n", "warn\n"));
    }

    [Fact]
    public async Task Shell_Result_SerializesAsExitCodeStdoutStderr()
    {
        // Arrange — this is the "result" object a JSON-RPC client receives
        SetupDockerExec("out\n", "err\n", exitCode: 1);

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "sh" }, _testContainerId);
        using var document = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(result));

        // Assert
        var payload = document.RootElement.GetProperty("result");
        payload.EnumerateObject().Select(p => p.Name).Should().Equal("exitCode", "stdout", "stderr");
        payload.GetProperty("exitCode").GetInt32().Should().Be(1);
        payload.GetProperty("stdout").GetString().Should().Be("out\n");
        payload.GetProperty("stderr").GetString().Should().Be("err\n");
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Shell_WhenTheDaemonReportsNoExitCode_FailsWithoutAResult()
    {
        // Arrange — an unknown exit code must not be reported as success (or as any exit code)
        SetupDockerExec("out\n", "", exitCode: 0);
        _dockerMock.Setup(x => x.Exec.InspectContainerExecAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerExecInspectResponse { ExitCode = null });

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "echo" }, _testContainerId);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().BeNull();
        result.Error.Should().Contain("no exit code");
    }

    [Fact]
    public async Task Shell_WhenTheExecCannotRun_FailsWithoutAResult()
    {
        // Arrange
        SetupDockerExecFailure();

        // Act
        var result = await _executor.ExecuteAsync(
            new ExecuteShellCommand { CommandName = "echo" }, _testContainerId);

        // Assert
        result.Success.Should().BeFalse();
        result.Result.Should().BeNull();
        result.Error.Should().Contain("Container exec failed");
    }

    #endregion

    #region DiffCommand Tests

    [Fact]
    public async Task Diff_ShouldGenerateUnifiedDiff_WhenContentsDiffer()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var modifiedContent = "line1\nline2-modified\nline3";

        var command = new DiffCommand
        {
            OriginalContent = originalContent,
            ModifiedContent = modifiedContent,
            ContextLines = 3
        };

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var diffResult = System.Text.Json.JsonSerializer.Deserialize<DiffResult>(
            System.Text.Json.JsonSerializer.Serialize(result.Result));

        diffResult.Should().NotBeNull();
        diffResult!.Diff.Should().Contain("-line2");
        diffResult.Diff.Should().Contain("+line2-modified");
        diffResult.AddedLines.Should().Be(1);
        diffResult.RemovedLines.Should().Be(1);
        diffResult.Identical.Should().BeFalse();
    }

    [Fact]
    public async Task Diff_ShouldReturnEmptyDiff_WhenContentsIdentical()
    {
        // Arrange
        var content = "line1\nline2\nline3";

        var command = new DiffCommand
        {
            OriginalContent = content,
            ModifiedContent = content
        };

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var diffResult = System.Text.Json.JsonSerializer.Deserialize<DiffResult>(
            System.Text.Json.JsonSerializer.Serialize(result.Result));

        diffResult.Should().NotBeNull();
        diffResult!.Identical.Should().BeTrue();
        diffResult.Diff.Should().BeEmpty();
    }

    [Fact]
    public async Task Diff_ShouldReadFromFiles_WhenPathsProvided()
    {
        // Arrange
        var command = new DiffCommand
        {
            OriginalPath = "old.txt",
            ModifiedPath = "new.txt",
            ContextLines = 3
        };

        // Setup file reads
        SetupDockerExecForFileRead("old.txt", "old content");
        SetupDockerExecForFileRead("new.txt", "new content");

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Diff_ShouldFail_WhenOriginalFileNotFound()
    {
        // Arrange
        var command = new DiffCommand
        {
            OriginalPath = "nonexistent.txt",
            ModifiedContent = "content"
        };

        SetupDockerExecFailure();

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed to read original file");
    }

    #endregion

    #region ApplyPatchCommand Tests

    [Fact]
    public async Task ApplyPatch_ShouldApplyPatch_WhenValidDiff()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        var command = new ApplyPatchCommand
        {
            Patch = patch,
            TargetPath = "test.txt",
            DryRun = false
        };

        // Setup file read and write
        SetupDockerExecForFileRead("test.txt", originalContent);
        SetupDockerExecForFileWrite("test.txt");

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var patchResult = System.Text.Json.JsonSerializer.Deserialize<PatchResult>(
            System.Text.Json.JsonSerializer.Serialize(result.Result));

        patchResult.Should().NotBeNull();
        patchResult!.Success.Should().BeTrue();
        patchResult.HunksApplied.Should().BeGreaterThan(0);
        patchResult.HunksFailed.Should().Be(0);
    }

    [Fact]
    public async Task ApplyPatch_ShouldNotModifyFile_WhenDryRun()
    {
        // Arrange
        var originalContent = "line1\nline2\nline3";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        var command = new ApplyPatchCommand
        {
            Patch = patch,
            TargetPath = "test.txt",
            DryRun = true
        };

        SetupDockerExecForFileRead("test.txt", originalContent);

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var patchResult = System.Text.Json.JsonSerializer.Deserialize<PatchResult>(
            System.Text.Json.JsonSerializer.Serialize(result.Result));

        patchResult.Should().NotBeNull();
        patchResult!.DryRun.Should().BeTrue();

        // Verify write was NOT called
        _dockerMock.Verify(
            x => x.Exec.CreateContainerExecAsync(
                It.IsAny<string>(),
                It.Is<ContainerExecCreateParameters>(p => p.Cmd.Contains("tee")),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyPatch_ShouldFail_WhenTargetFileNotFound()
    {
        // Arrange
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        var command = new ApplyPatchCommand
        {
            Patch = patch,
            TargetPath = "nonexistent.txt"
        };

        SetupDockerExecFailure();

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Target file not found");
    }

    [Fact]
    public async Task ApplyPatch_ShouldFail_WhenPatchCannotBeApplied()
    {
        // Arrange
        var originalContent = "completely different content";
        var patch = @"--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2-patched
 line3";

        var command = new ApplyPatchCommand
        {
            Patch = patch,
            TargetPath = "test.txt"
        };

        SetupDockerExecForFileRead("test.txt", originalContent);

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        // PatchApplicator will try fuzzy matching, but should fail
        var patchResult = System.Text.Json.JsonSerializer.Deserialize<PatchResult>(
            System.Text.Json.JsonSerializer.Serialize(result.Result));

        patchResult.Should().NotBeNull();
        patchResult!.HunksFailed.Should().BeGreaterThan(0);

        // A patch that did not apply is a failed command, not a successful one carrying a
        // failure inside its result.
        result.Success.Should().BeFalse();
        result.Error.Should().StartWith("Failed to apply patch");
    }

    #endregion

    #region Helper Methods

    private void SetupDockerExec(string stdout, string stderr, int exitCode = 0)
    {
        _dockerMock.Setup(x => x.Exec.InspectContainerExecAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerExecInspectResponse { ExitCode = exitCode });

        var execResponseMock = new ContainerExecCreateResponse { ID = "exec-123" };

        _dockerMock.Setup(x => x.Exec.CreateContainerExecAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerExecCreateParameters>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ContainerExecCreateParameters, CancellationToken>((_, p, _) => _execParams.Add(p))
            .ReturnsAsync(execResponseMock);

        // A real MultiplexedStream backed by an empty MemoryStream — CommandExecutor no longer
        // calls its Read/WriteAsync directly (those go through IContainerExecStreamIO, mocked
        // below), so this only needs to dispose safely. MultiplexedStream.Dispose() is
        // non-virtual and unconditionally disposes its backing stream, so Moq cannot intercept
        // it and a null-backed stream would NullReferenceException on disposal.
        var stream = new MultiplexedStream(new MemoryStream(), false);

        _dockerMock.Setup(x => x.Exec.StartContainerExecAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerExecStartParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        _streamIOMock.Setup(io => io.ReadOutputAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync((stdout, stderr));
    }

    private void SetupDockerExecForFileRead(string path, string content)
    {
        var execResponseMock = new ContainerExecCreateResponse { ID = $"exec-read-{path}" };
        SetupExitCode(execResponseMock.ID, 0);

        _dockerMock.Setup(x => x.Exec.CreateContainerExecAsync(
                _testContainerId,
                It.Is<ContainerExecCreateParameters>(p =>
                    p.Cmd.Contains("cat") && p.Cmd.Contains(path)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResponseMock);

        // See SetupDockerExec for why this is a real MultiplexedStream, not a Moq mock.
        var stream = new MultiplexedStream(new MemoryStream(), false);

        _dockerMock.Setup(x => x.Exec.StartContainerExecAsync(
                execResponseMock.ID,
                It.IsAny<ContainerExecStartParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        _streamIOMock.Setup(io => io.ReadOutputAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync((content, ""));
    }

    private void SetupDockerExecForFileWrite(string path)
    {
        var execResponseMock = new ContainerExecCreateResponse { ID = $"exec-write-{path}" };
        SetupExitCode(execResponseMock.ID, 0);

        _dockerMock.Setup(x => x.Exec.CreateContainerExecAsync(
                _testContainerId,
                It.Is<ContainerExecCreateParameters>(p =>
                    p.Cmd.Contains("dd") && p.Cmd.Contains($"of={path}")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResponseMock);

        var stream = new MultiplexedStream(new MemoryStream(), false);

        _dockerMock.Setup(x => x.Exec.StartContainerExecAsync(
                execResponseMock.ID,
                It.IsAny<ContainerExecStartParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        _streamIOMock.Setup(io => io.ReadOutputAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("", ""));
    }

    // Every file command reads the exit code back from the daemon once the exec's output ends.
    private void SetupExitCode(string execId, int exitCode) =>
        _dockerMock.Setup(x => x.Exec.InspectContainerExecAsync(execId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerExecInspectResponse { ExitCode = exitCode });

    private void SetupDockerExecFailure()
    {
        _dockerMock.Setup(x => x.Exec.CreateContainerExecAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerExecCreateParameters>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container exec failed"));
    }

    #endregion
}

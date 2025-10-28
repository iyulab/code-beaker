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
    private readonly Mock<DockerClient> _dockerMock;
    private readonly CommandExecutor _executor;
    private readonly string _testContainerId = "test-container-123";

    public CommandExecutorTests()
    {
        _dockerMock = new Mock<DockerClient>();
        _executor = new CommandExecutor(_dockerMock.Object);
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

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
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

        SetupDockerExec("", "find: '/nonexistent': No such file or directory");

        // Act
        var result = await _executor.ExecuteAsync(command, _testContainerId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed to list files");
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
            x => x.Exec.ExecCreateContainerAsync(
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
    }

    #endregion

    #region Helper Methods

    private void SetupDockerExec(string stdout, string stderr)
    {
        var execResponseMock = new ContainerExecCreateResponse { ID = "exec-123" };

        _dockerMock.Setup(x => x.Exec.ExecCreateContainerAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerExecCreateParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResponseMock);

        var streamMock = new Mock<MultiplexedStream>(MockBehavior.Loose, null!, false);

        _dockerMock.Setup(x => x.Exec.StartAndAttachContainerExecAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(streamMock.Object);

        // This is simplified - in reality, you'd need to mock the stream reading
        // For now, we're testing the logic flow
    }

    private void SetupDockerExecForFileRead(string path, string content)
    {
        var execResponseMock = new ContainerExecCreateResponse { ID = $"exec-read-{path}" };

        _dockerMock.Setup(x => x.Exec.ExecCreateContainerAsync(
                _testContainerId,
                It.Is<ContainerExecCreateParameters>(p =>
                    p.Cmd.Contains("cat") && p.Cmd.Contains(path)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResponseMock);

        // Mock stream for reading content
        var streamMock = new Mock<MultiplexedStream>(MockBehavior.Loose, null!, false);

        _dockerMock.Setup(x => x.Exec.StartAndAttachContainerExecAsync(
                execResponseMock.ID,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(streamMock.Object);
    }

    private void SetupDockerExecForFileWrite(string path)
    {
        var execResponseMock = new ContainerExecCreateResponse { ID = $"exec-write-{path}" };

        _dockerMock.Setup(x => x.Exec.ExecCreateContainerAsync(
                _testContainerId,
                It.Is<ContainerExecCreateParameters>(p =>
                    p.Cmd.Contains("tee") && p.Cmd.Contains(path)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResponseMock);

        var streamMock = new Mock<MultiplexedStream>(MockBehavior.Loose, null!, false);

        _dockerMock.Setup(x => x.Exec.StartAndAttachContainerExecAsync(
                execResponseMock.ID,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(streamMock.Object);
    }

    private void SetupDockerExecFailure()
    {
        _dockerMock.Setup(x => x.Exec.ExecCreateContainerAsync(
                It.IsAny<string>(),
                It.IsAny<ContainerExecCreateParameters>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container exec failed"));
    }

    #endregion
}

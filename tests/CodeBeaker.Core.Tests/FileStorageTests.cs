using CodeBeaker.Core.Storage;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Core.Tests;

public sealed class FileStorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileStorage _storage;

    public FileStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"codebeaker_test_{Guid.NewGuid()}");
        _storage = new FileStorage(Path.Combine(_testDir, "executions"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public async Task UpdateStatus_CreatesStatusFile()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();

        // Act
        await _storage.UpdateStatusAsync(executionId, "running");

        // Assert
        var statusFile = Path.Combine(_testDir, "executions", executionId, "status.json");
        File.Exists(statusFile).Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_ReturnsUpdatedStatus()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();
        await _storage.UpdateStatusAsync(executionId, "running");

        // Act
        var result = await _storage.GetStatusAsync(executionId);

        // Assert
        result.Should().NotBeNull();
        result!.ExecutionId.Should().Be(executionId);
        result.Status.Should().Be("running");
    }

    [Fact]
    public async Task SaveResult_SavesOutputFiles()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();
        var stdout = "Hello World\n";
        var stderr = "Warning: test\n";

        // Act
        await _storage.SaveResultAsync(
            executionId,
            stdout,
            stderr,
            exitCode: 0,
            durationMs: 1234);

        // Assert
        var execDir = Path.Combine(_testDir, "executions", executionId);
        var stdoutFile = Path.Combine(execDir, "stdout.txt");
        var stderrFile = Path.Combine(execDir, "stderr.txt");

        File.Exists(stdoutFile).Should().BeTrue();
        File.Exists(stderrFile).Should().BeTrue();

        var savedStdout = await File.ReadAllTextAsync(stdoutFile);
        var savedStderr = await File.ReadAllTextAsync(stderrFile);

        savedStdout.Should().Be(stdout);
        savedStderr.Should().Be(stderr);
    }

    [Fact]
    public async Task GetResult_ReturnsCompleteResult()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();
        await _storage.SaveResultAsync(
            executionId,
            "Output text",
            "Error text",
            exitCode: 0,
            durationMs: 500);

        // Act
        var result = await _storage.GetResultAsync(executionId);

        // Assert
        result.Should().NotBeNull();
        result!.ExecutionId.Should().Be(executionId);
        result.Status.Should().Be("completed");
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Be("Output text");
        result.Stderr.Should().Be("Error text");
        result.DurationMs.Should().Be(500);
    }

    [Fact]
    public async Task SaveResult_WithNonZeroExitCode_StatusIsFailed()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();

        // Act
        await _storage.SaveResultAsync(
            executionId,
            "",
            "SyntaxError",
            exitCode: 1,
            durationMs: 100);

        var result = await _storage.GetResultAsync(executionId);

        // Assert
        result!.Status.Should().Be("failed");
        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task SaveResult_WithTimeout_SetsTimeoutFlag()
    {
        // Arrange
        var executionId = Guid.NewGuid().ToString();

        // Act
        await _storage.SaveResultAsync(
            executionId,
            "partial output",
            "",
            exitCode: 124,
            durationMs: 5000,
            timeout: true,
            errorType: "timeout_error");

        var result = await _storage.GetResultAsync(executionId);

        // Assert
        result!.Timeout.Should().BeTrue();
        result.ErrorType.Should().Be("timeout_error");
        result.Status.Should().Be("failed");
    }

    [Fact]
    public async Task GetStatus_NonExistentExecution_ReturnsNull()
    {
        // Act
        var result = await _storage.GetStatusAsync("nonexistent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetResult_NonExistentExecution_ReturnsNull()
    {
        // Act
        var result = await _storage.GetResultAsync("nonexistent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveResult_UpdatesMetrics()
    {
        // Arrange
        var id1 = Guid.NewGuid().ToString();
        var id2 = Guid.NewGuid().ToString();

        // Act
        await _storage.SaveResultAsync(id1, "", "", 0, 100);
        await _storage.SaveResultAsync(id2, "", "", 1, 200);

        // Assert
        var metricsFile = Path.Combine(_testDir, "metrics", "counters.json");
        File.Exists(metricsFile).Should().BeTrue();

        var json = await File.ReadAllTextAsync(metricsFile);
        json.Should().Contain("total_processed");
        json.Should().Contain("status_completed");
        json.Should().Contain("status_failed");
    }
}

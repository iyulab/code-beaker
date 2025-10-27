using CodeBeaker.Core.Models;
using CodeBeaker.Core.Queue;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Core.Tests;

public sealed class FileQueueTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileQueue _queue;

    public FileQueueTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"codebeaker_test_{Guid.NewGuid()}");
        _queue = new FileQueue(Path.Combine(_testDir, "queue"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public async Task SubmitTask_CreatesTaskFile()
    {
        // Arrange
        var code = "print('Hello World')";
        var language = "python";
        var config = new ExecutionConfig { Timeout = 10 };

        // Act
        var executionId = await _queue.SubmitTaskAsync(code, language, config);

        // Assert
        executionId.Should().NotBeNullOrEmpty();
        var pendingDir = Path.Combine(_testDir, "queue", "pending");
        var files = Directory.GetFiles(pendingDir, "*.json");
        files.Should().HaveCount(1);
        files[0].Should().Contain(executionId);
    }

    [Fact]
    public async Task GetTask_ReturnsSubmittedTask()
    {
        // Arrange
        var code = "print('Test')";
        var language = "python";
        var config = new ExecutionConfig { Timeout = 5 };
        var executionId = await _queue.SubmitTaskAsync(code, language, config);

        // Act
        var task = await _queue.GetTaskAsync(timeout: 1);

        // Assert
        task.Should().NotBeNull();
        task!.ExecutionId.Should().Be(executionId);
        task.Code.Should().Be(code);
        task.Language.Should().Be(language);
        task.Config.Timeout.Should().Be(5);
    }

    [Fact]
    public async Task GetTask_ReturnsNullWhenNoTasks()
    {
        // Act
        var task = await _queue.GetTaskAsync(timeout: 1);

        // Assert
        task.Should().BeNull();
    }

    [Fact]
    public async Task GetTask_ReturnsFIFO()
    {
        // Arrange
        var id1 = await _queue.SubmitTaskAsync("code1", "python", new ExecutionConfig());
        await Task.Delay(100); // Ensure different timestamps
        var id2 = await _queue.SubmitTaskAsync("code2", "python", new ExecutionConfig());
        await Task.Delay(100);
        var id3 = await _queue.SubmitTaskAsync("code3", "python", new ExecutionConfig());

        // Act
        var task1 = await _queue.GetTaskAsync();
        var task2 = await _queue.GetTaskAsync();
        var task3 = await _queue.GetTaskAsync();

        // Assert
        task1!.ExecutionId.Should().Be(id1);
        task2!.ExecutionId.Should().Be(id2);
        task3!.ExecutionId.Should().Be(id3);
    }

    [Fact]
    public async Task CompleteTask_RemovesProcessingFile()
    {
        // Arrange
        var executionId = await _queue.SubmitTaskAsync("code", "python", new ExecutionConfig());
        var task = await _queue.GetTaskAsync();

        // Act
        await _queue.CompleteTaskAsync(executionId);

        // Assert
        var processingDir = Path.Combine(_testDir, "queue", "processing");
        var files = Directory.GetFiles(processingDir, $"*{executionId}.json");
        files.Should().BeEmpty();
    }

    // Note: Concurrent worker test moved to CodeBeaker.Integration.Tests.FileQueueConcurrencyTests
    // Timing-sensitive concurrency tests are more appropriate as integration tests
}

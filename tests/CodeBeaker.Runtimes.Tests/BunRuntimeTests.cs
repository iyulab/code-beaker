using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Bun;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// Bun Runtime 통합 테스트
/// </summary>
public sealed class BunRuntimeTests
{
    private readonly BunRuntime _runtime;

    public BunRuntimeTests()
    {
        _runtime = new BunRuntime();
    }

    [Fact]
    public void Runtime_ShouldHaveCorrectProperties()
    {
        // Assert
        Assert.Equal("bun", _runtime.Name);
        Assert.Equal(RuntimeType.Bun, _runtime.Type);
        Assert.Contains("bun", _runtime.SupportedEnvironments);
        Assert.Contains("typescript", _runtime.SupportedEnvironments);
        Assert.Contains("javascript", _runtime.SupportedEnvironments);
        Assert.Contains("nodejs", _runtime.SupportedEnvironments);
    }

    [Fact]
    public void Runtime_ShouldReturnCapabilities()
    {
        // Act
        var capabilities = _runtime.GetCapabilities();

        // Assert
        Assert.NotNull(capabilities);
        Assert.True(capabilities.StartupTimeMs < 100); // Bun은 100ms 이하
        Assert.True(capabilities.MemoryOverheadMB < 50); // Bun은 50MB 이하
        Assert.True(capabilities.IsolationLevel >= 7); // 권한 기반 샌드박스
        Assert.True(capabilities.SupportsFilesystemPersistence);
        Assert.True(capabilities.SupportsNetworkAccess);
    }

    [Fact(Skip = "Bun 설치 필요 - 선택적 런타임")]
    public async Task IsAvailableAsync_ShouldReturnTrue_WhenBunInstalled()
    {
        // Act
        var isAvailable = await _runtime.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable);
    }

    [Fact(Skip = "Bun 설치 필요 - 선택적 런타임")]
    public async Task CreateEnvironmentAsync_ShouldCreateEnvironment()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bun-test-{Guid.NewGuid():N}"),
            Permissions = new PermissionSettings
            {
                AllowRead = new List<string> { Path.GetTempPath() },
                AllowWrite = new List<string> { Path.GetTempPath() },
                AllowNet = false
            }
        };

        try
        {
            // Act
            var environment = await _runtime.CreateEnvironmentAsync(config);

            // Assert
            Assert.NotNull(environment);
            Assert.Equal(RuntimeType.Bun, environment.RuntimeType);
            Assert.Equal(EnvironmentState.Ready, environment.State);
            Assert.NotEmpty(environment.EnvironmentId);

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }
    }

    [Fact(Skip = "Bun 설치 필요 - 선택적 런타임")]
    public async Task ExecuteCodeCommand_ShouldRunJavaScript()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bun-test-{Guid.NewGuid():N}")
        };

        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = @"
                    const message = 'Hello from Bun!';
                    console.log(message);
                "
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success, result.Error);
            Assert.Contains("Hello from Bun!", result.Result?.ToString() ?? string.Empty);

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }
    }

    [Fact(Skip = "Bun 설치 필요 - 선택적 런타임")]
    public async Task ExecuteCodeCommand_ShouldRunTypeScript()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bun-test-{Guid.NewGuid():N}")
        };

        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = @"
                    const message: string = 'Hello TypeScript from Bun!';
                    console.log(message);
                "
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success, result.Error);
            Assert.Contains("Hello TypeScript from Bun!", result.Result?.ToString() ?? string.Empty);

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }
    }

    [Fact(Skip = "Bun 설치 필요 - 선택적 런타임")]
    public async Task WriteAndReadFile_ShouldMaintainFilesystemState()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bun-test-{Guid.NewGuid():N}")
        };

        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(config);

            var writeCommand = new WriteFileCommand
            {
                Path = "test.txt",
                Content = "Bun filesystem test",
                Mode = FileWriteMode.Create
            };

            // Act - Write
            var writeResult = await environment.ExecuteAsync(writeCommand);
            Assert.True(writeResult.Success, writeResult.Error);

            var readCommand = new ReadFileCommand
            {
                Path = "test.txt"
            };

            // Act - Read
            var readResult = await environment.ExecuteAsync(readCommand);

            // Assert
            Assert.True(readResult.Success, readResult.Error);
            Assert.Equal("Bun filesystem test", readResult.Result?.ToString());

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }
    }
}

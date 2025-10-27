using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Deno;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// Deno Runtime 통합 테스트
/// </summary>
public sealed class DenoRuntimeTests
{
    private readonly DenoRuntime _runtime;

    public DenoRuntimeTests()
    {
        _runtime = new DenoRuntime();
    }

    [Fact]
    public void Runtime_ShouldHaveCorrectProperties()
    {
        // Assert
        Assert.Equal("deno", _runtime.Name);
        Assert.Equal(RuntimeType.Deno, _runtime.Type);
        Assert.Contains("deno", _runtime.SupportedEnvironments);
        Assert.Contains("typescript", _runtime.SupportedEnvironments);
        Assert.Contains("javascript", _runtime.SupportedEnvironments);
    }

    [Fact]
    public void Runtime_ShouldReturnCapabilities()
    {
        // Act
        var capabilities = _runtime.GetCapabilities();

        // Assert
        Assert.NotNull(capabilities);
        Assert.True(capabilities.StartupTimeMs < 200); // Deno는 200ms 이하
        Assert.True(capabilities.MemoryOverheadMB < 100); // Deno는 100MB 이하
        Assert.True(capabilities.IsolationLevel >= 7); // 권한 기반 샌드박스
        Assert.True(capabilities.SupportsFilesystemPersistence);
        Assert.True(capabilities.SupportsNetworkAccess);
    }

    [Fact(Skip = "Deno 설치 경로를 찾을 수 없음 - 사용자 환경에서 Deno 경로 확인 필요")]
    public async Task IsAvailableAsync_ShouldReturnTrue_WhenDenoInstalled()
    {
        // Act
        var isAvailable = await _runtime.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable);
    }

    [Fact(Skip = "Deno 설치 경로를 찾을 수 없음 - 사용자 환경에서 Deno 경로 확인 필요")]
    public async Task CreateEnvironmentAsync_ShouldCreateEnvironment()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"deno-test-{Guid.NewGuid():N}"),
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
            Assert.Equal(RuntimeType.Deno, environment.RuntimeType);
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

    [Fact(Skip = "Deno 설치 경로를 찾을 수 없음 - 사용자 환경에서 Deno 경로 확인 필요")]
    public async Task ExecuteCodeCommand_ShouldRunTypeScript()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"deno-test-{Guid.NewGuid():N}")
        };

        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = @"
                    const message: string = 'Hello from Deno!';
                    console.log(message);
                "
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success, result.Error);
            Assert.Contains("Hello from Deno!", result.Result?.ToString() ?? string.Empty);

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

    [Fact(Skip = "Deno 설치 경로를 찾을 수 없음 - 사용자 환경에서 Deno 경로 확인 필요")]
    public async Task WriteAndReadFile_ShouldMaintainFilesystemState()
    {
        // Arrange
        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"deno-test-{Guid.NewGuid():N}")
        };

        try
        {
            var environment = await _runtime.CreateEnvironmentAsync(config);

            var writeCommand = new WriteFileCommand
            {
                Path = "test.txt",
                Content = "Deno filesystem test",
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
            Assert.Equal("Deno filesystem test", readResult.Result?.ToString());

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

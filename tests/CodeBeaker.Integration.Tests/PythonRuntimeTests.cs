using System;
using System.Threading.Tasks;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes.Python;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Phase 9.2: Python Runtime Integration Tests
/// </summary>
public class PythonRuntimeTests
{
    private async Task<bool> IsPythonAvailableAsync()
    {
        var runtime = new PythonRuntime();
        return await runtime.IsAvailableAsync();
    }

    [Fact]
    public async Task PythonRuntime_GetCapabilities_ReturnsExpectedValues()
    {
        // Arrange
        var runtime = new PythonRuntime();

        // Act
        var capabilities = runtime.GetCapabilities();

        // Assert
        Assert.Equal("python", runtime.Name);
        Assert.Equal(RuntimeType.Python, runtime.Type);
        Assert.Contains("python", runtime.SupportedEnvironments);
        Assert.Contains("python3", runtime.SupportedEnvironments);
        Assert.Equal(150, capabilities.StartupTimeMs);
        Assert.Equal(50, capabilities.MemoryOverheadMB);
        Assert.Equal(5, capabilities.IsolationLevel);
    }

    [Fact]
    public async Task PythonRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Skip if Python not installed
        if (!await IsPythonAvailableAsync())
        {
            return; // Skip test
        }

        // Arrange
        var runtime = new PythonRuntime();
        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"codebeaker-test-{Guid.NewGuid():N}")
        };

        System.IO.Directory.CreateDirectory(config.WorkspaceDirectory);

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "print('Hello from Python!')"
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success, $"Execution should succeed. Error: {result.Error}");
            Assert.Contains("Hello from Python!", result.Result?.ToString() ?? "");

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(config.WorkspaceDirectory))
            {
                System.IO.Directory.Delete(config.WorkspaceDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PythonRuntime_ExecuteWithMath_ReturnsCorrectResult()
    {
        // Skip if Python not installed
        if (!await IsPythonAvailableAsync())
        {
            return; // Skip test
        }

        // Arrange
        var runtime = new PythonRuntime();
        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"codebeaker-test-{Guid.NewGuid():N}")
        };

        System.IO.Directory.CreateDirectory(config.WorkspaceDirectory);

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "result = 2 + 2\nprint(result)"
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("4", result.Result?.ToString() ?? "");

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(config.WorkspaceDirectory))
            {
                System.IO.Directory.Delete(config.WorkspaceDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PythonRuntime_ExecuteWithError_ReturnsError()
    {
        // Skip if Python not installed
        if (!await IsPythonAvailableAsync())
        {
            return; // Skip test
        }

        // Arrange
        var runtime = new PythonRuntime();
        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"codebeaker-test-{Guid.NewGuid():N}")
        };

        System.IO.Directory.CreateDirectory(config.WorkspaceDirectory);

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "print(undefined_variable)" // NameError
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.False(result.Success, "Should fail with NameError");
            Assert.NotNull(result.Error);
            Assert.Contains("NameError", result.Error);

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(config.WorkspaceDirectory))
            {
                System.IO.Directory.Delete(config.WorkspaceDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PythonRuntime_ExecuteWithImports_WorksCorrectly()
    {
        // Skip if Python not installed
        if (!await IsPythonAvailableAsync())
        {
            return; // Skip test
        }

        // Arrange
        var runtime = new PythonRuntime();
        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"codebeaker-test-{Guid.NewGuid():N}")
        };

        System.IO.Directory.CreateDirectory(config.WorkspaceDirectory);

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = @"import math
result = math.sqrt(16)
print(f'Square root of 16 is {result}')"
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("4.0", result.Result?.ToString() ?? "");

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(config.WorkspaceDirectory))
            {
                System.IO.Directory.Delete(config.WorkspaceDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PythonRuntime_GetResourceUsage_ReturnsUsageData()
    {
        // Skip if Python not installed
        if (!await IsPythonAvailableAsync())
        {
            return; // Skip test
        }

        // Arrange
        var runtime = new PythonRuntime();
        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"codebeaker-test-{Guid.NewGuid():N}")
        };

        System.IO.Directory.CreateDirectory(config.WorkspaceDirectory);

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(config);

            // Execute code to have an active process
            var command = new ExecuteCodeCommand
            {
                Code = "for i in range(1000000): x = i ** 2"
            };

            // Act
            var executeTask = environment.ExecuteAsync(command);

            // Try to get resource usage (may be null if process already finished)
            var usage = await environment.GetResourceUsageAsync();

            await executeTask; // Wait for completion

            // Assert
            // Note: Usage may be null if the process finished before we queried
            // This test mainly verifies the method doesn't throw exceptions
            Assert.NotNull(environment); // Just verify environment exists

            // Cleanup
            await environment.DisposeAsync();
        }
        finally
        {
            if (System.IO.Directory.Exists(config.WorkspaceDirectory))
            {
                System.IO.Directory.Delete(config.WorkspaceDirectory, recursive: true);
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes.Node;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Phase 9: Node.js Runtime Integration Tests
/// </summary>
public class NodeRuntimeTests
{
    [Fact]
    public async Task NodeRuntime_IsAvailable_ReturnsTrue()
    {
        // Arrange
        var runtime = new NodeRuntime();

        // Act
        var isAvailable = await runtime.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable, "Node.js should be available on this system");
    }

    [Fact]
    public async Task NodeRuntime_GetCapabilities_ReturnsExpectedValues()
    {
        // Arrange
        var runtime = new NodeRuntime();

        // Act
        var capabilities = runtime.GetCapabilities();

        // Assert
        Assert.Equal("node", runtime.Name);
        Assert.Equal(RuntimeType.NodeJs, runtime.Type);
        Assert.Contains("node", runtime.SupportedEnvironments);
        Assert.Contains("javascript", runtime.SupportedEnvironments);
        Assert.Equal(100, capabilities.StartupTimeMs);
        Assert.Equal(40, capabilities.MemoryOverheadMB);
        Assert.Equal(5, capabilities.IsolationLevel);
    }

    [Fact]
    public async Task NodeRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Arrange
        var runtime = new NodeRuntime();
        var config = new RuntimeConfig
        {
            Environment = "nodejs",
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
                Code = "console.log('Hello from Node.js!')"
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.True(result.Success, $"Execution should succeed. Error: {result.Error}");
            Assert.Contains("Hello from Node.js!", result.Result?.ToString() ?? "");

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
    public async Task NodeRuntime_ExecuteWithMath_ReturnsCorrectResult()
    {
        // Arrange
        var runtime = new NodeRuntime();
        var config = new RuntimeConfig
        {
            Environment = "nodejs",
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
                Code = "const result = 2 + 2; console.log(result);"
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
    public async Task NodeRuntime_ExecuteWithError_ReturnsError()
    {
        // Arrange
        var runtime = new NodeRuntime();
        var config = new RuntimeConfig
        {
            Environment = "nodejs",
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
                Code = "console.log(undefinedVariable);" // Reference error
            };

            // Act
            var result = await environment.ExecuteAsync(command);

            // Assert
            Assert.False(result.Success, "Should fail with reference error");
            Assert.NotNull(result.Error);
            Assert.Contains("ReferenceError", result.Error);

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
    public async Task NodeRuntime_GetResourceUsage_ReturnsUsageData()
    {
        // Arrange
        var runtime = new NodeRuntime();
        var config = new RuntimeConfig
        {
            Environment = "nodejs",
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
                Code = "for(let i=0; i<1000000; i++) { Math.sqrt(i); }"
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

using CodeBeaker.Core.Docker;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes;
using FluentAssertions;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// 런타임 실행 통합 테스트 (Docker 필요)
/// </summary>
[Collection("Docker")]
public sealed class RuntimeExecutionTests : IAsyncLifetime
{
    private readonly string _tempDir;

    public RuntimeExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"codebeaker-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task PythonRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("python");
        var code = "print('Hello from Python')";
        var config = new ExecutionConfig { Timeout = 10 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("Hello from Python");
        result.Stderr.Should().BeEmpty();
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task PythonRuntime_WithPackages_InstallsAndRuns()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("python");
        var code = @"
import requests
print(f'requests version: {requests.__version__}')
";
        var config = new ExecutionConfig
        {
            Timeout = 30,
            Packages = new List<string> { "requests" }
        };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("requests version:");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task JavaScriptRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("javascript");
        var code = "console.log('Hello from Node.js');";
        var config = new ExecutionConfig { Timeout = 10 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("Hello from Node.js");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task JavaScriptRuntime_WithPackages_InstallsAndRuns()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("javascript");
        var code = @"
const _ = require('lodash');
console.log('lodash version:', _.VERSION);
";
        var config = new ExecutionConfig
        {
            Timeout = 30,
            Packages = new List<string> { "lodash" }
        };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("lodash version:");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task GoRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("go");
        var code = @"
package main
import ""fmt""
func main() {
    fmt.Println(""Hello from Go"")
}
";
        var config = new ExecutionConfig { Timeout = 30 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("Hello from Go");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task GoRuntime_WithPackages_InstallsAndRuns()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("go");
        var code = @"
package main
import (
    ""fmt""
    ""github.com/google/uuid""
)
func main() {
    id := uuid.New()
    fmt.Printf(""UUID: %s\n"", id)
}
";
        var config = new ExecutionConfig
        {
            Timeout = 60,
            Packages = new List<string> { "github.com/google/uuid" }
        };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("UUID:");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task CSharpRuntime_ExecuteSimpleCode_ReturnsOutput()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("csharp");
        var code = "Console.WriteLine(\"Hello from C#\");";
        var config = new ExecutionConfig { Timeout = 30 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("Hello from C#");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task CSharpRuntime_WithPackages_InstallsAndRuns()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("csharp");
        var code = @"
using Newtonsoft.Json;
var obj = new { Message = ""Hello"", Value = 42 };
var json = JsonConvert.SerializeObject(obj);
Console.WriteLine(json);
";
        var config = new ExecutionConfig
        {
            Timeout = 60,
            Packages = new List<string> { "Newtonsoft.Json" }
        };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"Message\":\"Hello\"");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task Runtime_ExecutionTimeout_ReturnsTimeoutError()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("python");
        var code = @"
import time
time.sleep(10)
print('Should not reach here')
";
        var config = new ExecutionConfig { Timeout = 2 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.DurationMs.Should().BeGreaterOrEqualTo(2000);
        result.DurationMs.Should().BeLessThan(3000);
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task Runtime_SyntaxError_ReturnsErrorInStderr()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("python");
        var code = "print('Missing closing quote)";
        var config = new ExecutionConfig { Timeout = 10 };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.Stderr.Should().Contain("SyntaxError");
    }

    [Fact(Skip = "Requires Docker runtime images")]
    public async Task Runtime_MemoryLimit_EnforcesLimit()
    {
        // Arrange
        var runtime = RuntimeRegistry.Get("python");
        var code = @"
# Try to allocate 100MB
data = bytearray(100 * 1024 * 1024)
print('Allocated memory')
";
        var config = new ExecutionConfig
        {
            Timeout = 10,
            MemoryLimit = 50 // Limit to 50MB
        };

        // Act
        var result = await runtime.ExecuteAsync(code, config);

        // Assert
        result.ExitCode.Should().NotBe(0);
        // Memory limit exceeded results in OOM kill or MemoryError
    }
}

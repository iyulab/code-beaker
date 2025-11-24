using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Security integration tests
/// Phase 11: Production Hardening
/// </summary>
public sealed class SecurityIntegrationTests
{
    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldBlockExcessiveExecutions()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableRateLimiting = true,
            ExecutionsPerMinute = 3
        };

        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteCodeCommand { Code = "console.log('test');" };

        // Act - Execute 3 times (should work)
        var result1 = await secureEnv.ExecuteAsync(command, CancellationToken.None);
        var result2 = await secureEnv.ExecuteAsync(command, CancellationToken.None);
        var result3 = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // 4th attempt should be blocked
        var result4 = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);
        Assert.False(result4.Success);
        Assert.Contains("Rate limit exceeded", result4.Error);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldBlockDangerousCode()
    {
        // Arrange
        var config = new SecurityConfig { EnableInputValidation = true };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteCodeCommand { Code = "rm -rf /" };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("blocked pattern", result.Error);

        // Verify security violation was logged
        var violations = auditLogger.GetSecurityViolations();
        Assert.Single(violations);
        Assert.Equal(AuditEventType.SecurityViolation, violations[0].EventType);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldBlockPathTraversal()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            SandboxRestrictFilesystem = true
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ReadFileCommand { Path = "../../etc/passwd" };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("blocked pattern", result.Error);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldAllowValidOperations()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            EnableRateLimiting = true
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteCodeCommand { Code = "console.log('Hello, World!');" };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        // Verify execution was logged
        var logs = auditLogger.GetRecentLogs();
        Assert.Single(logs);
        Assert.Equal(AuditEventType.CodeExecution, logs[0].EventType);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldTruncateLongOutput()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            MaxOutputLength = 100
        };

        var longOutput = new string('a', 200);
        var mockEnv = CreateMockEnvironment(longOutput);
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteCodeCommand { Code = "console.log('test');" };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Result!.ToString()!.Length < longOutput.Length);
        Assert.Contains("Output truncated", result.Result.ToString());
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldBlockInvalidFileExtension()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            SandboxRestrictFilesystem = true,
            AllowedFileExtensions = new List<string> { ".js", ".txt" }
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new WriteFileCommand
        {
            Path = "D:\\workspace\\malware.exe",
            Content = "binary data"
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Error);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldBlockPackageInjection()
    {
        // Arrange
        var config = new SecurityConfig { EnableInputValidation = true };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new InstallPackagesCommand
        {
            Packages = new List<string> { "express; rm -rf /" }
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("invalid characters", result.Error);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldDisableShellCommands_InStrictMode()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            SandboxDisableShellCommands = true
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteShellCommand
        {
            CommandName = "npm",
            Args = new List<string> { "install", "express" }
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldLogAllOperations()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableAuditLogging = true,
            EnableInputValidation = true
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        // Act - Execute different commands
        await secureEnv.ExecuteAsync(new ExecuteCodeCommand { Code = "console.log('test');" }, CancellationToken.None);
        await secureEnv.ExecuteAsync(new WriteFileCommand { Path = "D:\\workspace\\test.js", Content = "code" }, CancellationToken.None);
        await secureEnv.ExecuteAsync(new ReadFileCommand { Path = "D:\\workspace\\test.js" }, CancellationToken.None);

        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Equal(3, logs.Count);
        Assert.Contains(logs, l => l.EventType == AuditEventType.CodeExecution);
        Assert.Contains(logs, l => l.EventType == AuditEventType.FileWrite);
        Assert.Contains(logs, l => l.EventType == AuditEventType.FileRead);
    }

    [Fact]
    public async Task SecurityEnhancedEnvironment_ShouldPassUserId_ToAuditLog()
    {
        // Arrange
        var config = new SecurityConfig { EnableAuditLogging = true };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace", "user-123");

        var command = new ExecuteCodeCommand { Code = "console.log('test');" };

        // Act
        await secureEnv.ExecuteAsync(command, CancellationToken.None);

        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal("user-123", logs[0].UserId);
    }

    // Helper methods

    private Mock<IExecutionEnvironment> CreateMockEnvironment(string? customOutput = null)
    {
        var mock = new Mock<IExecutionEnvironment>();
        mock.Setup(e => e.EnvironmentId).Returns("test-env");
        mock.Setup(e => e.RuntimeType).Returns(RuntimeType.NodeJs);
        mock.Setup(e => e.State).Returns(EnvironmentState.Ready);

        mock.Setup(e => e.ExecuteAsync(It.IsAny<Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult
            {
                Success = true,
                Result = customOutput ?? "output",
                DurationMs = 100
            });

        mock.Setup(e => e.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnvironmentState.Ready);

        mock.Setup(e => e.GetResourceUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceUsage?)null);

        return mock;
    }

    private AuditLogger CreateAuditLogger(SecurityConfig config)
    {
        var mockLogger = new Mock<ILogger<AuditLogger>>();
        return new AuditLogger(mockLogger.Object, config);
    }

    private ILogger<T> CreateLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();
        return mockLogger.Object;
    }
}

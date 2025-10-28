using CodeBeaker.Core.Models;
using CodeBeaker.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Security;

/// <summary>
/// AuditLogger 단위 테스트
/// Phase 11: Production Hardening
/// </summary>
public sealed class AuditLoggerTests
{
    private readonly Mock<ILogger<AuditLogger>> _mockLogger;
    private readonly SecurityConfig _defaultConfig;

    public AuditLoggerTests()
    {
        _mockLogger = new Mock<ILogger<AuditLogger>>();
        _defaultConfig = new SecurityConfig { EnableAuditLogging = true };
    }

    [Fact]
    public void LogEvent_ShouldNotLog_WhenAuditLoggingDisabled()
    {
        // Arrange
        var config = new SecurityConfig { EnableAuditLogging = false };
        var auditLogger = new AuditLogger(_mockLogger.Object, config);
        var auditLog = new AuditLog
        {
            SessionId = "session-1",
            EventType = AuditEventType.CodeExecution,
            Description = "Test execution"
        };

        // Act
        auditLogger.LogEvent(auditLog);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Empty(logs);
    }

    [Fact]
    public void LogEvent_ShouldAddToQueue_WhenAuditLoggingEnabled()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        var auditLog = new AuditLog
        {
            SessionId = "session-1",
            EventType = AuditEventType.CodeExecution,
            Description = "Test execution"
        };

        // Act
        auditLogger.LogEvent(auditLog);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal("session-1", logs[0].SessionId);
        Assert.Equal(AuditEventType.CodeExecution, logs[0].EventType);
    }

    [Fact]
    public void LogEvent_ShouldLogToILogger()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        var auditLog = new AuditLog
        {
            SessionId = "session-1",
            UserId = "user-1",
            EventType = AuditEventType.SecurityViolation,
            Severity = AuditSeverity.Error,
            Description = "Test violation",
            Success = false
        };

        // Act
        auditLogger.LogEvent(auditLog);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void LogCodeExecution_ShouldCreateAuditLog()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act
        auditLogger.LogCodeExecution("session-1", "user-1", true, 150, null);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal(AuditEventType.CodeExecution, logs[0].EventType);
        Assert.Equal("session-1", logs[0].SessionId);
        Assert.Equal("user-1", logs[0].UserId);
        Assert.True(logs[0].Success);
        Assert.Equal(150, logs[0].DurationMs);
    }

    [Fact]
    public void LogFileOperation_ShouldCreateAuditLog()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act
        auditLogger.LogFileOperation("session-1", "user-1", AuditEventType.FileWrite,
            "D:\\workspace\\test.js", true, null);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal(AuditEventType.FileWrite, logs[0].EventType);
        Assert.Contains("test.js", logs[0].Description);
        Assert.True(logs[0].Metadata.ContainsKey("filePath"));
    }

    [Fact]
    public void LogSecurityViolation_ShouldCreateCriticalLog()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act
        auditLogger.LogSecurityViolation("session-1", "user-1",
            "Directory traversal attempt", "../../etc/passwd");
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal(AuditEventType.SecurityViolation, logs[0].EventType);
        Assert.Equal(AuditSeverity.Error, logs[0].Severity);
        Assert.False(logs[0].Success);
        Assert.Contains("Directory traversal", logs[0].Description);
    }

    [Fact]
    public void LogRateLimitExceeded_ShouldIncludeMetadata()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act
        auditLogger.LogRateLimitExceeded("session-1", "user-1", 60, 65);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal(AuditEventType.RateLimitExceeded, logs[0].EventType);
        Assert.Equal(60, logs[0].Metadata["limit"]);
        Assert.Equal(65, logs[0].Metadata["count"]);
    }

    [Fact]
    public void LogPackageInstall_ShouldIncludePackageList()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        var packages = new List<string> { "express", "lodash", "@types/node" };

        // Act
        auditLogger.LogPackageInstall("session-1", "user-1", packages, true, 5000, null);
        var logs = auditLogger.GetRecentLogs();

        // Assert
        Assert.Single(logs);
        Assert.Equal(AuditEventType.PackageInstall, logs[0].EventType);
        Assert.Contains("express", logs[0].Description);
        Assert.True(logs[0].Metadata.ContainsKey("packages"));
        Assert.Equal(5000, logs[0].DurationMs);
    }

    [Fact]
    public void GetRecentLogs_ShouldReturnLastNEntries()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        for (int i = 0; i < 10; i++)
        {
            auditLogger.LogCodeExecution($"session-{i}", null, true, 100, null);
        }

        // Act
        var logs = auditLogger.GetRecentLogs(5);

        // Assert
        Assert.Equal(5, logs.Count);
        Assert.Equal("session-9", logs[^1].SessionId); // Last one should be session-9
    }

    [Fact]
    public void GetSessionLogs_ShouldFilterBySession()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        auditLogger.LogCodeExecution("session-1", null, true, 100, null);
        auditLogger.LogCodeExecution("session-2", null, true, 100, null);
        auditLogger.LogCodeExecution("session-1", null, true, 100, null);

        // Act
        var logs = auditLogger.GetSessionLogs("session-1");

        // Assert
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal("session-1", log.SessionId));
    }

    [Fact]
    public void GetSecurityViolations_ShouldFilterSecurityEvents()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        auditLogger.LogCodeExecution("session-1", null, true, 100, null);
        auditLogger.LogSecurityViolation("session-1", null, "Test violation", "details");
        auditLogger.LogRateLimitExceeded("session-1", null, 60, 61);
        auditLogger.LogCodeExecution("session-1", null, true, 100, null);

        // Act
        var violations = auditLogger.GetSecurityViolations();

        // Assert
        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.EventType == AuditEventType.SecurityViolation);
        Assert.Contains(violations, v => v.EventType == AuditEventType.RateLimitExceeded);
    }

    [Fact]
    public void LogEvent_ShouldLimitQueueSize()
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act - Add more than 10000 entries
        for (int i = 0; i < 11000; i++)
        {
            auditLogger.LogCodeExecution($"session-{i}", null, true, 100, null);
        }

        var logs = auditLogger.GetRecentLogs(20000);

        // Assert - Should be capped at 10000
        Assert.True(logs.Count <= 10000);
    }

    [Fact]
    public void AuditLog_ShouldHaveAutoGeneratedId()
    {
        // Arrange & Act
        var log = new AuditLog
        {
            SessionId = "session-1",
            EventType = AuditEventType.CodeExecution
        };

        // Assert
        Assert.NotNull(log.Id);
        Assert.NotEqual(Guid.Empty.ToString(), log.Id);
    }

    [Fact]
    public void AuditLog_ShouldHaveAutoGeneratedTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var log = new AuditLog
        {
            SessionId = "session-1",
            EventType = AuditEventType.CodeExecution
        };

        var after = DateTime.UtcNow;

        // Assert
        Assert.True(log.Timestamp >= before);
        Assert.True(log.Timestamp <= after);
    }

    [Theory]
    [InlineData(AuditSeverity.Info, LogLevel.Information)]
    [InlineData(AuditSeverity.Warning, LogLevel.Warning)]
    [InlineData(AuditSeverity.Error, LogLevel.Error)]
    [InlineData(AuditSeverity.Critical, LogLevel.Critical)]
    public void LogEvent_ShouldMapSeverityToLogLevel(AuditSeverity severity, LogLevel expectedLogLevel)
    {
        // Arrange
        var auditLogger = new AuditLogger(_mockLogger.Object, _defaultConfig);
        var auditLog = new AuditLog
        {
            SessionId = "session-1",
            EventType = AuditEventType.CodeExecution,
            Severity = severity,
            Description = "Test"
        };

        // Act
        auditLogger.LogEvent(auditLog);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                expectedLogLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void MultipleLoggers_ShouldMaintainSeparateQueues()
    {
        // Arrange
        var logger1 = new AuditLogger(_mockLogger.Object, _defaultConfig);
        var logger2 = new AuditLogger(_mockLogger.Object, _defaultConfig);

        // Act
        logger1.LogCodeExecution("session-1", null, true, 100, null);
        logger2.LogCodeExecution("session-2", null, true, 100, null);

        // Assert
        Assert.Single(logger1.GetRecentLogs());
        Assert.Single(logger2.GetRecentLogs());
        Assert.Equal("session-1", logger1.GetRecentLogs()[0].SessionId);
        Assert.Equal("session-2", logger2.GetRecentLogs()[0].SessionId);
    }
}

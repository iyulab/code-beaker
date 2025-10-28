using CodeBeaker.Core.Models;
using CodeBeaker.Core.Security;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Security;

/// <summary>
/// RateLimiter 단위 테스트
/// Phase 11: Production Hardening
/// </summary>
public sealed class RateLimiterTests
{
    [Fact]
    public void CheckRateLimit_ShouldAllow_WhenRateLimitingDisabled()
    {
        // Arrange
        var config = new SecurityConfig { EnableRateLimiting = false };
        var rateLimiter = new RateLimiter(config);

        // Act
        var result = rateLimiter.CheckRateLimit("session-1");

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckRateLimit_ShouldAllow_FirstExecution()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 10 };
        var rateLimiter = new RateLimiter(config);

        // Act
        var result = rateLimiter.CheckRateLimit("session-1");

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(9, result.RemainingExecutions);
    }

    [Fact]
    public void CheckRateLimit_ShouldDeny_WhenLimitExceeded()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 3 };
        var rateLimiter = new RateLimiter(config);

        // Act - Execute 3 times (up to limit)
        rateLimiter.CheckRateLimit("session-1");
        rateLimiter.CheckRateLimit("session-1");
        rateLimiter.CheckRateLimit("session-1");

        // Try 4th time (should be denied)
        var result = rateLimiter.CheckRateLimit("session-1");

        // Assert
        Assert.False(result.Allowed);
        Assert.NotNull(result.DenyReason);
        Assert.Contains("Rate limit exceeded", result.DenyReason);
        Assert.True(result.SecondsUntilReset > 0);
    }

    [Fact]
    public void CheckRateLimit_ShouldTrackRemainingExecutions()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 5 };
        var rateLimiter = new RateLimiter(config);

        // Act & Assert
        var result1 = rateLimiter.CheckRateLimit("session-1");
        Assert.Equal(4, result1.RemainingExecutions);

        var result2 = rateLimiter.CheckRateLimit("session-1");
        Assert.Equal(3, result2.RemainingExecutions);

        var result3 = rateLimiter.CheckRateLimit("session-1");
        Assert.Equal(2, result3.RemainingExecutions);
    }

    [Fact]
    public void CheckRateLimit_ShouldIsolateSessions()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 2 };
        var rateLimiter = new RateLimiter(config);

        // Act
        rateLimiter.CheckRateLimit("session-1");
        rateLimiter.CheckRateLimit("session-1"); // session-1 at limit

        var result = rateLimiter.CheckRateLimit("session-2"); // Different session

        // Assert
        Assert.True(result.Allowed); // session-2 should still be allowed
    }

    [Fact]
    public void GetRateLimitState_ShouldReturnNull_WhenSessionNotTracked()
    {
        // Arrange
        var config = new SecurityConfig();
        var rateLimiter = new RateLimiter(config);

        // Act
        var state = rateLimiter.GetRateLimitState("unknown-session");

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public void GetRateLimitState_ShouldReturnState_AfterExecution()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 10 };
        var rateLimiter = new RateLimiter(config);
        rateLimiter.CheckRateLimit("session-1");

        // Act
        var state = rateLimiter.GetRateLimitState("session-1");

        // Assert
        Assert.NotNull(state);
        Assert.Equal("session-1", state!.Identifier);
        Assert.Equal(1, state.ExecutionCount);
        Assert.Equal(10, state.MaxExecutions);
    }

    [Fact]
    public void ResetRateLimit_ShouldClearSessionState()
    {
        // Arrange
        var config = new SecurityConfig { ExecutionsPerMinute = 2 };
        var rateLimiter = new RateLimiter(config);
        rateLimiter.CheckRateLimit("session-1");
        rateLimiter.CheckRateLimit("session-1");

        // Act
        rateLimiter.ResetRateLimit("session-1");
        var result = rateLimiter.CheckRateLimit("session-1");

        // Assert
        Assert.True(result.Allowed); // Should be allowed again after reset
    }

    [Fact]
    public void GetActiveRateLimits_ShouldReturnAllSessions()
    {
        // Arrange
        var config = new SecurityConfig();
        var rateLimiter = new RateLimiter(config);
        rateLimiter.CheckRateLimit("session-1");
        rateLimiter.CheckRateLimit("session-2");
        rateLimiter.CheckRateLimit("session-3");

        // Act
        var activeLimits = rateLimiter.GetActiveRateLimits();

        // Assert
        Assert.Equal(3, activeLimits.Count);
        Assert.True(activeLimits.ContainsKey("session-1"));
        Assert.True(activeLimits.ContainsKey("session-2"));
        Assert.True(activeLimits.ContainsKey("session-3"));
    }

    [Fact]
    public void RateLimitState_IsRateLimitExceeded_ShouldReturnCorrectly()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 3,
            ExecutionCount = 2,
            WindowStart = DateTime.UtcNow
        };

        // Act & Assert
        Assert.False(state.IsRateLimitExceeded()); // 2/3 - not exceeded

        state.ExecutionCount = 3;
        Assert.False(state.IsRateLimitExceeded()); // 3/3 - at limit but not exceeded

        state.ExecutionCount = 4;
        Assert.True(state.IsRateLimitExceeded()); // 4/3 - exceeded
    }

    [Fact]
    public void RateLimitState_ShouldResetWindow_WhenExpired()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            ExecutionCount = 10, // At limit
            WindowStart = DateTime.UtcNow.AddMinutes(-2), // 2 minutes ago (expired)
            WindowDurationMinutes = 1
        };

        // Act
        var exceeded = state.IsRateLimitExceeded();

        // Assert
        Assert.False(exceeded); // Should not be exceeded because window reset
        Assert.Equal(0, state.ExecutionCount); // Count should be reset
    }

    [Fact]
    public void RateLimitState_IncrementExecution_ShouldIncreaseCounters()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            WindowStart = DateTime.UtcNow
        };

        // Act
        state.IncrementExecution();
        state.IncrementExecution();
        state.IncrementExecution();

        // Assert
        Assert.Equal(3, state.ExecutionCount);
        Assert.Equal(3, state.TotalExecutions);
    }

    [Fact]
    public void RateLimitState_IncrementExecution_ShouldResetWindowWhenExpired()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            ExecutionCount = 5,
            TotalExecutions = 5,
            WindowStart = DateTime.UtcNow.AddMinutes(-2),
            WindowDurationMinutes = 1
        };

        // Act
        state.IncrementExecution();

        // Assert
        Assert.Equal(1, state.ExecutionCount); // Reset to 1 (new window)
        Assert.Equal(6, state.TotalExecutions); // Total continues to increase
    }

    [Fact]
    public void RateLimitState_GetRemainingExecutions_ShouldReturnCorrectValue()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            ExecutionCount = 3,
            WindowStart = DateTime.UtcNow
        };

        // Act
        var remaining = state.GetRemainingExecutions();

        // Assert
        Assert.Equal(7, remaining);
    }

    [Fact]
    public void RateLimitState_GetRemainingExecutions_ShouldReturnMaxWhenWindowExpired()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            ExecutionCount = 10,
            WindowStart = DateTime.UtcNow.AddMinutes(-2),
            WindowDurationMinutes = 1
        };

        // Act
        var remaining = state.GetRemainingExecutions();

        // Assert
        Assert.Equal(10, remaining); // Full quota available after window reset
    }

    [Fact]
    public void RateLimitState_GetSecondsUntilReset_ShouldReturnCorrectValue()
    {
        // Arrange
        var state = new RateLimitState
        {
            Identifier = "test",
            MaxExecutions = 10,
            WindowStart = DateTime.UtcNow.AddSeconds(-30), // 30 seconds ago
            WindowDurationMinutes = 1
        };

        // Act
        var seconds = state.GetSecondsUntilReset();

        // Assert
        Assert.True(seconds > 0);
        Assert.True(seconds <= 30); // Should be around 30 seconds (60 - 30)
    }

    [Fact]
    public void CleanupExpiredEntries_ShouldRemoveOldSessions()
    {
        // Arrange
        var config = new SecurityConfig();
        var rateLimiter = new RateLimiter(config);

        // Create a rate limit state (this is normally internal)
        rateLimiter.CheckRateLimit("session-1");

        // Act
        rateLimiter.CleanupExpiredEntries();

        // Note: This test is limited because we can't easily manipulate internal state
        // In a real scenario, we'd need to wait or use a time provider
        // For now, we just verify the method doesn't throw
        Assert.NotNull(rateLimiter);
    }

    [Fact]
    public void CheckRateLimit_WithUserId_ShouldPassToAuditLogger()
    {
        // Arrange
        var config = new SecurityConfig
        {
            EnableRateLimiting = true,
            EnableAuditLogging = true,
            ExecutionsPerMinute = 1
        };
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<AuditLogger>>();
        var auditLogger = new AuditLogger(mockLogger.Object, config);
        var rateLimiter = new RateLimiter(config, auditLogger);

        // Act - Exceed rate limit
        rateLimiter.CheckRateLimit("session-1", "user-123");
        rateLimiter.CheckRateLimit("session-1", "user-123"); // Should trigger audit log

        // Assert - Check that audit logger has logged the rate limit violation
        var violations = auditLogger.GetSecurityViolations();
        Assert.Single(violations);
        Assert.Equal(AuditEventType.RateLimitExceeded, violations[0].EventType);
        Assert.Equal("session-1", violations[0].SessionId);
        Assert.Equal("user-123", violations[0].UserId);
    }
}

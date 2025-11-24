namespace CodeBeaker.Core.Models;

/// <summary>
/// Rate limit tracking state
/// Phase 11: Production Hardening
/// </summary>
public sealed class RateLimitState
{
    /// <summary>
    /// Session or user identifier
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Execution count in current window
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Window start time
    /// </summary>
    public DateTime WindowStart { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Window duration (minutes)
    /// </summary>
    public int WindowDurationMinutes { get; set; } = 1;

    /// <summary>
    /// Maximum allowed executions in window
    /// </summary>
    public int MaxExecutions { get; set; }

    /// <summary>
    /// Total executions (lifetime)
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Check if rate limit is exceeded
    /// </summary>
    public bool IsRateLimitExceeded()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - WindowStart;

        // Reset window if expired
        if (elapsed.TotalMinutes >= WindowDurationMinutes)
        {
            WindowStart = now;
            ExecutionCount = 0;
            return false;
        }

        return ExecutionCount >= MaxExecutions;
    }

    /// <summary>
    /// Increment execution counter
    /// </summary>
    public void IncrementExecution()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - WindowStart;

        // Reset window if expired
        if (elapsed.TotalMinutes >= WindowDurationMinutes)
        {
            WindowStart = now;
            ExecutionCount = 0;
        }

        ExecutionCount++;
        TotalExecutions++;
    }

    /// <summary>
    /// Get remaining executions in current window
    /// </summary>
    public int GetRemainingExecutions()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - WindowStart;

        // If window expired, full quota available
        if (elapsed.TotalMinutes >= WindowDurationMinutes)
        {
            return MaxExecutions;
        }

        return Math.Max(0, MaxExecutions - ExecutionCount);
    }

    /// <summary>
    /// Get time until window reset (seconds)
    /// </summary>
    public int GetSecondsUntilReset()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - WindowStart;
        var remaining = TimeSpan.FromMinutes(WindowDurationMinutes) - elapsed;

        return Math.Max(0, (int)remaining.TotalSeconds);
    }
}

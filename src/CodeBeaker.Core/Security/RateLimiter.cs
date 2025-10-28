using System.Collections.Concurrent;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Security;

/// <summary>
/// Rate limiting service
/// Phase 11: Production Hardening
/// </summary>
public sealed class RateLimiter
{
    private readonly SecurityConfig _config;
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimits = new();
    private readonly AuditLogger? _auditLogger;

    public RateLimiter(SecurityConfig config, AuditLogger? auditLogger = null)
    {
        _config = config;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Check if execution is allowed for session
    /// </summary>
    public RateLimitResult CheckRateLimit(string sessionId, string? userId = null)
    {
        if (!_config.EnableRateLimiting)
        {
            return RateLimitResult.Allow();
        }

        var identifier = sessionId;
        var state = _rateLimits.GetOrAdd(identifier, _ => new RateLimitState
        {
            Identifier = identifier,
            MaxExecutions = _config.ExecutionsPerMinute,
            WindowDurationMinutes = 1
        });

        if (state.IsRateLimitExceeded())
        {
            _auditLogger?.LogRateLimitExceeded(sessionId, userId, state.MaxExecutions, state.ExecutionCount);

            return RateLimitResult.Deny(
                $"Rate limit exceeded: {state.ExecutionCount}/{state.MaxExecutions} executions per minute",
                state.GetSecondsUntilReset(),
                state.GetRemainingExecutions()
            );
        }

        state.IncrementExecution();
        return RateLimitResult.Allow(state.GetRemainingExecutions());
    }

    /// <summary>
    /// Get rate limit state for session
    /// </summary>
    public RateLimitState? GetRateLimitState(string sessionId)
    {
        _rateLimits.TryGetValue(sessionId, out var state);
        return state;
    }

    /// <summary>
    /// Reset rate limit for session
    /// </summary>
    public void ResetRateLimit(string sessionId)
    {
        _rateLimits.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Clean up expired rate limit entries
    /// </summary>
    public void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _rateLimits
            .Where(kvp => (now - kvp.Value.WindowStart).TotalMinutes > 60) // Remove after 1 hour
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _rateLimits.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get all active rate limits
    /// </summary>
    public Dictionary<string, RateLimitState> GetActiveRateLimits()
    {
        return _rateLimits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}

/// <summary>
/// Rate limit check result
/// </summary>
public sealed class RateLimitResult
{
    public bool Allowed { get; set; }
    public string? DenyReason { get; set; }
    public int SecondsUntilReset { get; set; }
    public int RemainingExecutions { get; set; }

    public static RateLimitResult Allow(int remaining = -1) => new()
    {
        Allowed = true,
        RemainingExecutions = remaining
    };

    public static RateLimitResult Deny(string reason, int secondsUntilReset, int remaining) => new()
    {
        Allowed = false,
        DenyReason = reason,
        SecondsUntilReset = secondsUntilReset,
        RemainingExecutions = remaining
    };
}

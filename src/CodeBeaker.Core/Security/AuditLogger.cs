using System.Collections.Concurrent;
using CodeBeaker.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Core.Security;

/// <summary>
/// Audit logging service for security events
/// Phase 11: Production Hardening
/// </summary>
public sealed class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly SecurityConfig _config;
    private readonly ConcurrentQueue<AuditLog> _auditLogs = new();
    private readonly object _lockObject = new();

    public AuditLogger(ILogger<AuditLogger> logger, SecurityConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Log audit event
    /// </summary>
    public void LogEvent(AuditLog auditLog)
    {
        if (!_config.EnableAuditLogging)
        {
            return;
        }

        // Add to in-memory queue
        _auditLogs.Enqueue(auditLog);

        // Clean old logs periodically (keep last 10000 entries in memory)
        if (_auditLogs.Count > 10000)
        {
            lock (_lockObject)
            {
                while (_auditLogs.Count > 10000)
                {
                    _auditLogs.TryDequeue(out _);
                }
            }
        }

        // Log to structured logger
        var logLevel = auditLog.Severity switch
        {
            AuditSeverity.Critical => LogLevel.Critical,
            AuditSeverity.Error => LogLevel.Error,
            AuditSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "[AUDIT] {EventType} | Session={SessionId} | User={UserId} | Success={Success} | {Description}",
            auditLog.EventType, auditLog.SessionId, auditLog.UserId, auditLog.Success, auditLog.Description);
    }

    /// <summary>
    /// Log code execution event
    /// </summary>
    public void LogCodeExecution(string sessionId, string? userId, bool success, int durationMs, string? error = null)
    {
        LogEvent(new AuditLog
        {
            SessionId = sessionId,
            UserId = userId,
            EventType = AuditEventType.CodeExecution,
            Severity = success ? AuditSeverity.Info : AuditSeverity.Warning,
            CommandType = "execute_code",
            Description = success ? "Code executed successfully" : $"Code execution failed: {error}",
            Success = success,
            Error = error,
            DurationMs = durationMs
        });
    }

    /// <summary>
    /// Log file operation event
    /// </summary>
    public void LogFileOperation(string sessionId, string? userId, AuditEventType eventType,
        string filePath, bool success, string? error = null)
    {
        var description = eventType switch
        {
            AuditEventType.FileRead => $"File read: {filePath}",
            AuditEventType.FileWrite => $"File write: {filePath}",
            AuditEventType.FileDelete => $"File delete: {filePath}",
            AuditEventType.DirectoryCreate => $"Directory create: {filePath}",
            _ => $"File operation: {filePath}"
        };

        LogEvent(new AuditLog
        {
            SessionId = sessionId,
            UserId = userId,
            EventType = eventType,
            Severity = success ? AuditSeverity.Info : AuditSeverity.Warning,
            Description = success ? description : $"{description} - Failed: {error}",
            Success = success,
            Error = error,
            Metadata = new Dictionary<string, object> { ["filePath"] = filePath }
        });
    }

    /// <summary>
    /// Log security violation
    /// </summary>
    public void LogSecurityViolation(string sessionId, string? userId, string violation, string details)
    {
        LogEvent(new AuditLog
        {
            SessionId = sessionId,
            UserId = userId,
            EventType = AuditEventType.SecurityViolation,
            Severity = AuditSeverity.Error,
            Description = violation,
            Success = false,
            Error = details,
            Metadata = new Dictionary<string, object> { ["violation"] = violation }
        });
    }

    /// <summary>
    /// Log rate limit exceeded
    /// </summary>
    public void LogRateLimitExceeded(string sessionId, string? userId, int limit, int count)
    {
        LogEvent(new AuditLog
        {
            SessionId = sessionId,
            UserId = userId,
            EventType = AuditEventType.RateLimitExceeded,
            Severity = AuditSeverity.Warning,
            Description = $"Rate limit exceeded: {count}/{limit}",
            Success = false,
            Metadata = new Dictionary<string, object>
            {
                ["limit"] = limit,
                ["count"] = count
            }
        });
    }

    /// <summary>
    /// Log package installation
    /// </summary>
    public void LogPackageInstall(string sessionId, string? userId, List<string> packages,
        bool success, int durationMs, string? error = null)
    {
        LogEvent(new AuditLog
        {
            SessionId = sessionId,
            UserId = userId,
            EventType = AuditEventType.PackageInstall,
            Severity = success ? AuditSeverity.Info : AuditSeverity.Warning,
            CommandType = "install_packages",
            Description = $"Package install: {string.Join(", ", packages)}",
            Success = success,
            Error = error,
            DurationMs = durationMs,
            Metadata = new Dictionary<string, object> { ["packages"] = packages }
        });
    }

    /// <summary>
    /// Get recent audit logs
    /// </summary>
    public List<AuditLog> GetRecentLogs(int count = 100)
    {
        return _auditLogs.TakeLast(count).ToList();
    }

    /// <summary>
    /// Get audit logs for specific session
    /// </summary>
    public List<AuditLog> GetSessionLogs(string sessionId)
    {
        return _auditLogs.Where(log => log.SessionId == sessionId).ToList();
    }

    /// <summary>
    /// Get security violations
    /// </summary>
    public List<AuditLog> GetSecurityViolations(int count = 50)
    {
        return _auditLogs
            .Where(log => log.EventType == AuditEventType.SecurityViolation ||
                          log.EventType == AuditEventType.RateLimitExceeded ||
                          log.EventType == AuditEventType.InputValidationFailure)
            .TakeLast(count)
            .ToList();
    }
}

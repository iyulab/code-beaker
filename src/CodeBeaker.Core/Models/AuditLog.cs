using System.Text.Json.Serialization;

namespace CodeBeaker.Core.Models;

/// <summary>
/// Audit log entry for security tracking
/// Phase 11: Production Hardening
/// </summary>
public sealed class AuditLog
{
    /// <summary>
    /// Log entry ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// User identifier (IP, user ID, etc.)
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Event type (execution, file_operation, security_violation, etc.)
    /// </summary>
    [JsonPropertyName("eventType")]
    public AuditEventType EventType { get; set; }

    /// <summary>
    /// Event severity
    /// </summary>
    [JsonPropertyName("severity")]
    public AuditSeverity Severity { get; set; }

    /// <summary>
    /// Command type executed
    /// </summary>
    [JsonPropertyName("commandType")]
    public string? CommandType { get; set; }

    /// <summary>
    /// Event description
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Success/failure status
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Execution duration (milliseconds)
    /// </summary>
    [JsonPropertyName("durationMs")]
    public int? DurationMs { get; set; }
}

/// <summary>
/// Audit event types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditEventType
{
    CodeExecution,
    FileRead,
    FileWrite,
    FileDelete,
    DirectoryCreate,
    ShellCommand,
    PackageInstall,
    SessionCreate,
    SessionDestroy,
    SecurityViolation,
    RateLimitExceeded,
    InputValidationFailure
}

/// <summary>
/// Audit severity levels
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

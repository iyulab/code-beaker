using System.Text.Json.Serialization;

namespace CodeBeaker.Core.Models;

/// <summary>
/// Security configuration for production hardening
/// Phase 11: Production Hardening
/// </summary>
public sealed class SecurityConfig
{
    /// <summary>
    /// Enable input validation and sanitization
    /// </summary>
    [JsonPropertyName("enableInputValidation")]
    public bool EnableInputValidation { get; set; } = true;

    /// <summary>
    /// Enable rate limiting
    /// </summary>
    [JsonPropertyName("enableRateLimiting")]
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Enable audit logging
    /// </summary>
    [JsonPropertyName("enableAuditLogging")]
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Maximum code length (characters)
    /// </summary>
    [JsonPropertyName("maxCodeLength")]
    public int MaxCodeLength { get; set; } = 100_000; // 100KB of code

    /// <summary>
    /// Maximum output length (characters)
    /// </summary>
    [JsonPropertyName("maxOutputLength")]
    public int MaxOutputLength { get; set; } = 1_000_000; // 1MB output

    /// <summary>
    /// Allowed file extensions for file operations
    /// </summary>
    [JsonPropertyName("allowedFileExtensions")]
    public List<string> AllowedFileExtensions { get; set; } = new()
    {
        ".py", ".js", ".ts", ".txt", ".json", ".yaml", ".yml", ".md",
        ".html", ".css", ".jsx", ".tsx", ".sh", ".bash"
    };

    /// <summary>
    /// Blocked path patterns (regex)
    /// </summary>
    [JsonPropertyName("blockedPathPatterns")]
    public List<string> BlockedPathPatterns { get; set; } = new()
    {
        @"\.\.\/", // Prevent directory traversal
        @"^\/etc", // System directories
        @"^\/sys",
        @"^\/proc",
        @"^C:\\Windows", // Windows system directories
        @"^C:\\Program Files"
    };

    /// <summary>
    /// Blocked command patterns (regex) for shell commands
    /// </summary>
    [JsonPropertyName("blockedCommandPatterns")]
    public List<string> BlockedCommandPatterns { get; set; } = new()
    {
        @"rm\s+-rf\s+\/", // Dangerous rm commands
        @"dd\s+if=", // Disk operations
        @"mkfs\.", // Format commands
        @"fork\s*\(\)", // Fork bombs
        @":\(\)\{.*\}.*:", // Shell fork bomb pattern
        @"sudo\s+", // Privilege escalation
        @"su\s+", // User switching
    };

    /// <summary>
    /// Maximum concurrent sessions per user/IP
    /// </summary>
    [JsonPropertyName("maxConcurrentSessions")]
    public int MaxConcurrentSessions { get; set; } = 10;

    /// <summary>
    /// Maximum executions per session
    /// </summary>
    [JsonPropertyName("maxExecutionsPerSession")]
    public int MaxExecutionsPerSession { get; set; } = 1000;

    /// <summary>
    /// Rate limit: executions per minute
    /// </summary>
    [JsonPropertyName("executionsPerMinute")]
    public int ExecutionsPerMinute { get; set; } = 60;

    /// <summary>
    /// Enable sandbox mode (stricter restrictions)
    /// </summary>
    [JsonPropertyName("enableSandbox")]
    public bool EnableSandbox { get; set; } = true;

    /// <summary>
    /// Sandbox: disable network access
    /// </summary>
    [JsonPropertyName("sandboxDisableNetwork")]
    public bool SandboxDisableNetwork { get; set; } = false; // Allow network by default for package installs

    /// <summary>
    /// Sandbox: restrict file system access to workspace only
    /// </summary>
    [JsonPropertyName("sandboxRestrictFilesystem")]
    public bool SandboxRestrictFilesystem { get; set; } = true;

    /// <summary>
    /// Sandbox: disable shell command execution
    /// </summary>
    [JsonPropertyName("sandboxDisableShellCommands")]
    public bool SandboxDisableShellCommands { get; set; } = false;

    /// <summary>
    /// Audit log retention days
    /// </summary>
    [JsonPropertyName("auditLogRetentionDays")]
    public int AuditLogRetentionDays { get; set; } = 90;
}

using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Commands.Models;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Core.Security;

/// <summary>
/// Security-enhanced execution environment wrapper
/// Adds input validation, rate limiting, and audit logging
/// Phase 11: Production Hardening
/// </summary>
public sealed class SecurityEnhancedEnvironment : IExecutionEnvironment
{
    private readonly IExecutionEnvironment _inner;
    private readonly SecurityConfig _security;
    private readonly RateLimiter _rateLimiter;
    private readonly AuditLogger _auditLogger;
    private readonly ILogger<SecurityEnhancedEnvironment> _logger;
    private readonly string _workspaceDirectory;
    private readonly string? _userId;

    public string EnvironmentId => _inner.EnvironmentId;
    public RuntimeType RuntimeType => _inner.RuntimeType;
    public EnvironmentState State => _inner.State;

    public SecurityEnhancedEnvironment(
        IExecutionEnvironment inner,
        SecurityConfig security,
        RateLimiter rateLimiter,
        AuditLogger auditLogger,
        ILogger<SecurityEnhancedEnvironment> logger,
        string workspaceDirectory,
        string? userId = null)
    {
        _inner = inner;
        _security = security;
        _rateLimiter = rateLimiter;
        _auditLogger = auditLogger;
        _logger = logger;
        _workspaceDirectory = workspaceDirectory;
        _userId = userId;
    }

    public async Task<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken = default)
    {
        var sessionId = EnvironmentId;

        // 1. Rate Limiting Check
        if (_security.EnableRateLimiting)
        {
            var rateLimitResult = _rateLimiter.CheckRateLimit(sessionId, _userId);
            if (!rateLimitResult.Allowed)
            {
                _logger.LogWarning("Rate limit exceeded for session {SessionId}", sessionId);

                return new CommandResult
                {
                    Success = false,
                    Error = rateLimitResult.DenyReason,
                    Result = $"Rate limit exceeded. Retry in {rateLimitResult.SecondsUntilReset} seconds."
                };
            }
        }

        // 2. Input Validation
        if (_security.EnableInputValidation)
        {
            var validationResult = ValidateCommand(command);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Input validation failed for session {SessionId}: {Error}",
                    sessionId, validationResult.ErrorMessage);

                _auditLogger.LogSecurityViolation(sessionId, _userId,
                    "Input Validation Failure", validationResult.ErrorMessage ?? "Unknown validation error");

                return new CommandResult
                {
                    Success = false,
                    Error = validationResult.ErrorMessage,
                    Result = string.Empty
                };
            }
        }

        // 3. Execute Command
        var startTime = DateTime.UtcNow;
        CommandResult result;

        try
        {
            result = await _inner.ExecuteAsync(command, cancellationToken);

            // 4. Sanitize Output
            if (_security.EnableInputValidation && result.Success)
            {
                if (result.Result is string resultStr)
                {
                    result.Result = InputValidator.SanitizeOutput(resultStr, _security);
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    result.Error = InputValidator.SanitizeOutput(result.Error, _security);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed for session {SessionId}", sessionId);

            result = new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }

        // 5. Audit Logging
        if (_security.EnableAuditLogging)
        {
            LogCommandExecution(sessionId, command, result);
        }

        return result;
    }

    private ValidationResult ValidateCommand(Command command)
    {
        return command switch
        {
            ExecuteCodeCommand code => InputValidator.ValidateCode(code.Code, _security),

            WriteFileCommand write => InputValidator.ValidateFilePath(write.Path, _workspaceDirectory, _security),

            ReadFileCommand read => InputValidator.ValidateFilePath(read.Path, _workspaceDirectory, _security),

            CopyFileCommand copy =>
                InputValidator.ValidateFilePath(copy.Source, _workspaceDirectory, _security) is { IsValid: false } srcResult ? srcResult :
                InputValidator.ValidateFilePath(copy.Destination, _workspaceDirectory, _security),

            CreateDirectoryCommand createDir => InputValidator.ValidateFilePath(createDir.Path, _workspaceDirectory, _security),

            ExecuteShellCommand shell => InputValidator.ValidateShellCommand(
                $"{shell.CommandName} {string.Join(" ", shell.Args)}", _security),

            InstallPackagesCommand install => InputValidator.ValidatePackageNames(install.Packages),

            _ => ValidationResult.Success()
        };
    }

    private void LogCommandExecution(string sessionId, Command command, CommandResult result)
    {
        switch (command)
        {
            case ExecuteCodeCommand:
                _auditLogger.LogCodeExecution(sessionId, _userId, result.Success,
                    result.DurationMs, result.Error);
                break;

            case WriteFileCommand write:
                _auditLogger.LogFileOperation(sessionId, _userId, AuditEventType.FileWrite,
                    write.Path, result.Success, result.Error);
                break;

            case ReadFileCommand read:
                _auditLogger.LogFileOperation(sessionId, _userId, AuditEventType.FileRead,
                    read.Path, result.Success, result.Error);
                break;

            case CreateDirectoryCommand createDir:
                _auditLogger.LogFileOperation(sessionId, _userId, AuditEventType.DirectoryCreate,
                    createDir.Path, result.Success, result.Error);
                break;

            case ExecuteShellCommand shell:
                _auditLogger.LogEvent(new AuditLog
                {
                    SessionId = sessionId,
                    UserId = _userId,
                    EventType = AuditEventType.ShellCommand,
                    Severity = result.Success ? AuditSeverity.Info : AuditSeverity.Warning,
                    CommandType = "shell",
                    Description = $"Shell command: {shell.CommandName} {string.Join(" ", shell.Args)}",
                    Success = result.Success,
                    Error = result.Error,
                    DurationMs = result.DurationMs
                });
                break;

            case InstallPackagesCommand install:
                _auditLogger.LogPackageInstall(sessionId, _userId, install.Packages,
                    result.Success, result.DurationMs, result.Error);
                break;
        }
    }

    public Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetStateAsync(cancellationToken);
    }

    public Task<ResourceUsage?> GetResourceUsageAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetResourceUsageAsync(cancellationToken);
    }

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        return _inner.CleanupAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }
}

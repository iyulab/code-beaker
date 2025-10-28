using System.Text.RegularExpressions;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Security;

/// <summary>
/// Input validation and sanitization utility
/// Phase 11: Production Hardening
/// </summary>
public static class InputValidator
{
    /// <summary>
    /// Validate code input
    /// </summary>
    public static ValidationResult ValidateCode(string code, SecurityConfig config)
    {
        if (string.IsNullOrEmpty(code))
        {
            return ValidationResult.Fail("Code cannot be empty");
        }

        if (code.Length > config.MaxCodeLength)
        {
            return ValidationResult.Fail($"Code exceeds maximum length of {config.MaxCodeLength} characters");
        }

        // Check for dangerous patterns in code
        foreach (var pattern in config.BlockedCommandPatterns)
        {
            try
            {
                if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
                {
                    return ValidationResult.Fail($"Code contains blocked pattern: {pattern}");
                }
            }
            catch
            {
                // Invalid regex pattern in config, skip
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate file path
    /// </summary>
    public static ValidationResult ValidateFilePath(string path, string workspaceDirectory, SecurityConfig config)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Fail("File path cannot be empty");
        }

        // Normalize path
        try
        {
            path = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return ValidationResult.Fail($"Invalid file path: {ex.Message}");
        }

        // Check if path is within workspace (sandbox)
        if (config.SandboxRestrictFilesystem)
        {
            var normalizedWorkspace = Path.GetFullPath(workspaceDirectory);
            if (!path.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Fail("File path must be within workspace directory");
            }
        }

        // Check blocked path patterns
        foreach (var pattern in config.BlockedPathPatterns)
        {
            try
            {
                if (Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
                {
                    return ValidationResult.Fail($"File path matches blocked pattern: {pattern}");
                }
            }
            catch
            {
                // Invalid regex pattern in config, skip
            }
        }

        // Check file extension
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension) &&
            config.AllowedFileExtensions.Count > 0 &&
            !config.AllowedFileExtensions.Contains(extension))
        {
            return ValidationResult.Fail($"File extension '{extension}' is not allowed");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate shell command
    /// </summary>
    public static ValidationResult ValidateShellCommand(string command, SecurityConfig config)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return ValidationResult.Fail("Shell command cannot be empty");
        }

        if (config.SandboxDisableShellCommands)
        {
            return ValidationResult.Fail("Shell commands are disabled in sandbox mode");
        }

        // Check for dangerous command patterns
        foreach (var pattern in config.BlockedCommandPatterns)
        {
            try
            {
                if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
                {
                    return ValidationResult.Fail($"Shell command contains blocked pattern: {pattern}");
                }
            }
            catch
            {
                // Invalid regex pattern in config, skip
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate package names
    /// </summary>
    public static ValidationResult ValidatePackageNames(List<string> packages)
    {
        if (packages == null || packages.Count == 0)
        {
            return ValidationResult.Success();
        }

        foreach (var package in packages)
        {
            if (string.IsNullOrWhiteSpace(package))
            {
                return ValidationResult.Fail("Package name cannot be empty");
            }

            // Check for command injection attempts
            if (package.Contains(';') || package.Contains('&') || package.Contains('|') ||
                package.Contains('`') || package.Contains('$') || package.Contains('\n'))
            {
                return ValidationResult.Fail($"Package name contains invalid characters: {package}");
            }

            // Basic length check
            if (package.Length > 200)
            {
                return ValidationResult.Fail($"Package name too long: {package}");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Sanitize output (truncate if too long)
    /// </summary>
    public static string SanitizeOutput(string output, SecurityConfig config)
    {
        if (string.IsNullOrEmpty(output))
        {
            return output;
        }

        if (output.Length > config.MaxOutputLength)
        {
            var truncatedLength = config.MaxOutputLength - 100;
            return output.Substring(0, truncatedLength) +
                   $"\n\n[Output truncated: {output.Length} chars total, showing first {truncatedLength}]";
        }

        return output;
    }

    /// <summary>
    /// Validate session limits
    /// </summary>
    public static ValidationResult ValidateSessionLimits(Session session, SecurityConfig config)
    {
        if (session.ExecutionCount >= config.MaxExecutionsPerSession)
        {
            return ValidationResult.Fail($"Session execution limit reached ({config.MaxExecutionsPerSession})");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Validation result
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}

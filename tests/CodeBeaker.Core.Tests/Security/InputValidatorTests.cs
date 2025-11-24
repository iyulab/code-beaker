using CodeBeaker.Core.Models;
using CodeBeaker.Core.Security;
using Xunit;

namespace CodeBeaker.Core.Tests.Security;

/// <summary>
/// InputValidator 단위 테스트
/// Phase 11: Production Hardening
/// </summary>
public sealed class InputValidatorTests
{
    private readonly SecurityConfig _defaultConfig;

    public InputValidatorTests()
    {
        _defaultConfig = new SecurityConfig();
    }

    #region Code Validation Tests

    [Fact]
    public void ValidateCode_ShouldFail_WhenCodeIsEmpty()
    {
        // Arrange
        var code = string.Empty;

        // Act
        var result = InputValidator.ValidateCode(code, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCode_ShouldFail_WhenCodeExceedsMaxLength()
    {
        // Arrange
        var code = new string('a', _defaultConfig.MaxCodeLength + 1);

        // Act
        var result = InputValidator.ValidateCode(code, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("exceeds maximum length", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCode_ShouldFail_WhenCodeContainsBlockedPattern()
    {
        // Arrange
        var code = "rm -rf /";

        // Act
        var result = InputValidator.ValidateCode(code, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("blocked pattern", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCode_ShouldFail_WhenCodeContainsForkBomb()
    {
        // Arrange
        var code = ":(){ :|:& };:";

        // Act
        var result = InputValidator.ValidateCode(code, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("blocked pattern", result.ErrorMessage);
    }

    [Fact]
    public void ValidateCode_ShouldSucceed_WithValidCode()
    {
        // Arrange
        var code = "console.log('Hello, World!');";

        // Act
        var result = InputValidator.ValidateCode(code, _defaultConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region File Path Validation Tests

    [Fact]
    public void ValidateFilePath_ShouldFail_WhenPathIsEmpty()
    {
        // Arrange
        var path = string.Empty;
        var workspace = "D:\\workspace";

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFilePath_ShouldFail_WhenPathContainsTraversal()
    {
        // Arrange
        var path = "../../etc/passwd";
        var workspace = "D:\\workspace";

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        // Path traversal should be caught by either workspace check or blocked pattern
        Assert.True(result.ErrorMessage.Contains("workspace directory") || result.ErrorMessage.Contains("blocked pattern"),
            $"Expected error about workspace or blocked pattern, got: {result.ErrorMessage}");
    }

    [Fact]
    public void ValidateFilePath_ShouldFail_WhenPathOutsideWorkspace()
    {
        // Arrange
        var path = "D:\\other\\file.txt";
        var workspace = "D:\\workspace";

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("within workspace", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFilePath_ShouldFail_WhenExtensionNotAllowed()
    {
        // Arrange
        var workspace = "D:\\workspace";
        var path = Path.Combine(workspace, "file.exe");

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFilePath_ShouldSucceed_WithValidPath()
    {
        // Arrange
        var workspace = "D:\\workspace";
        var path = Path.Combine(workspace, "script.js");

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateFilePath_ShouldSucceed_WithSubdirectory()
    {
        // Arrange
        var workspace = "D:\\workspace";
        var path = Path.Combine(workspace, "subdir", "file.py");

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, _defaultConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Shell Command Validation Tests

    [Fact]
    public void ValidateShellCommand_ShouldFail_WhenCommandIsEmpty()
    {
        // Arrange
        var command = string.Empty;

        // Act
        var result = InputValidator.ValidateShellCommand(command, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void ValidateShellCommand_ShouldFail_WhenShellCommandsDisabled()
    {
        // Arrange
        var config = new SecurityConfig { SandboxDisableShellCommands = true };
        var command = "echo hello";

        // Act
        var result = InputValidator.ValidateShellCommand(command, config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("disabled", result.ErrorMessage);
    }

    [Fact]
    public void ValidateShellCommand_ShouldFail_WithDangerousCommand()
    {
        // Arrange
        var command = "sudo apt-get install something";

        // Act
        var result = InputValidator.ValidateShellCommand(command, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("blocked pattern", result.ErrorMessage);
    }

    [Fact]
    public void ValidateShellCommand_ShouldSucceed_WithSafeCommand()
    {
        // Arrange
        var command = "npm install express";

        // Act
        var result = InputValidator.ValidateShellCommand(command, _defaultConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Package Name Validation Tests

    [Fact]
    public void ValidatePackageNames_ShouldSucceed_WithEmptyList()
    {
        // Arrange
        var packages = new List<string>();

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePackageNames_ShouldFail_WithEmptyPackageName()
    {
        // Arrange
        var packages = new List<string> { "express", "", "lodash" };

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageNames_ShouldFail_WithCommandInjection()
    {
        // Arrange
        var packages = new List<string> { "express; rm -rf /" };

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid characters", result.ErrorMessage);
    }

    [Theory]
    [InlineData("package&rm")]
    [InlineData("package|ls")]
    [InlineData("package`whoami`")]
    [InlineData("package$HOME")]
    [InlineData("package\nrm")]
    public void ValidatePackageNames_ShouldFail_WithInjectionCharacters(string packageName)
    {
        // Arrange
        var packages = new List<string> { packageName };

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid characters", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageNames_ShouldFail_WithTooLongName()
    {
        // Arrange
        var packages = new List<string> { new string('a', 201) };

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too long", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageNames_ShouldSucceed_WithValidPackages()
    {
        // Arrange
        var packages = new List<string> { "express", "@types/node", "lodash@4.17.21" };

        // Act
        var result = InputValidator.ValidatePackageNames(packages);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Output Sanitization Tests

    [Fact]
    public void SanitizeOutput_ShouldReturnOriginal_WhenWithinLimit()
    {
        // Arrange
        var output = "Hello, World!";

        // Act
        var result = InputValidator.SanitizeOutput(output, _defaultConfig);

        // Assert
        Assert.Equal(output, result);
    }

    [Fact]
    public void SanitizeOutput_ShouldTruncate_WhenExceedsLimit()
    {
        // Arrange
        var output = new string('a', _defaultConfig.MaxOutputLength + 1000);

        // Act
        var result = InputValidator.SanitizeOutput(output, _defaultConfig);

        // Assert
        Assert.True(result.Length < output.Length);
        Assert.Contains("Output truncated", result);
        Assert.Contains($"{output.Length} chars total", result);
    }

    [Fact]
    public void SanitizeOutput_ShouldHandleNull()
    {
        // Arrange
        string? output = null;

        // Act
        var result = InputValidator.SanitizeOutput(output!, _defaultConfig);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SanitizeOutput_ShouldHandleEmpty()
    {
        // Arrange
        var output = string.Empty;

        // Act
        var result = InputValidator.SanitizeOutput(output, _defaultConfig);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region Session Limits Validation Tests

    [Fact]
    public void ValidateSessionLimits_ShouldFail_WhenExecutionLimitReached()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "test-session",
            ExecutionCount = _defaultConfig.MaxExecutionsPerSession
        };

        // Act
        var result = InputValidator.ValidateSessionLimits(session, _defaultConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("limit reached", result.ErrorMessage);
    }

    [Fact]
    public void ValidateSessionLimits_ShouldSucceed_WhenBelowLimit()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "test-session",
            ExecutionCount = 50
        };

        // Act
        var result = InputValidator.ValidateSessionLimits(session, _defaultConfig);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public void ValidateCode_ShouldRespectCustomMaxLength()
    {
        // Arrange
        var config = new SecurityConfig { MaxCodeLength = 100 };
        var code = new string('a', 101);

        // Act
        var result = InputValidator.ValidateCode(code, config);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateFilePath_ShouldRespectCustomAllowedExtensions()
    {
        // Arrange
        var config = new SecurityConfig
        {
            AllowedFileExtensions = new List<string> { ".txt" }
        };
        var workspace = "D:\\workspace";
        var path = Path.Combine(workspace, "file.js");

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePath_ShouldAllowAnyExtension_WhenListEmpty()
    {
        // Arrange
        var config = new SecurityConfig
        {
            AllowedFileExtensions = new List<string>()
        };
        var workspace = "D:\\workspace";
        var path = Path.Combine(workspace, "file.exe");

        // Act
        var result = InputValidator.ValidateFilePath(path, workspace, config);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion
}

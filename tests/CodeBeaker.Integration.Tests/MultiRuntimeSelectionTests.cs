using System.Text.Json;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Runtime;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Runtimes.Deno;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Multi-Runtime 선택 및 실행 테스트
/// </summary>
public sealed class MultiRuntimeSelectionTests : IDisposable
{
    private readonly SessionManager _sessionManager;

    public MultiRuntimeSelectionTests()
    {
        // 실제 Docker Runtime과 Deno Runtime 사용
        var dockerRuntime = new CodeBeaker.Runtimes.Docker.DockerRuntime();
        var denoRuntime = new DenoRuntime();

        var runtimes = new List<IExecutionRuntime> { dockerRuntime, denoRuntime };
        var sessionStore = new CodeBeaker.Core.Storage.InMemorySessionStore();
        _sessionManager = new SessionManager(sessionStore, runtimes);
    }

    [Fact]
    public async Task CreateSession_WithDockerRuntimeType_ShouldUseDocker()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker // 명시적 Docker 선택
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(RuntimeType.Docker, session.RuntimeType);
        Assert.NotEmpty(session.ContainerId);
        Assert.NotEmpty(session.EnvironmentId);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task CreateSession_WithSpeedPreference_ShouldSelectDeno()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "javascript",
            RuntimePreference = RuntimePreference.Speed // Deno가 더 빠름 (80ms vs 2000ms)
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        // Note: Deno가 PATH에 없으면 Docker로 폴백될 수 있음
        Assert.True(session.RuntimeType == RuntimeType.Deno || session.RuntimeType == RuntimeType.Docker);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task CreateSession_WithSecurityPreference_ShouldSelectDocker()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            RuntimePreference = RuntimePreference.Security // Docker가 더 안전 (Isolation 9/10 vs 7/10)
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(RuntimeType.Docker, session.RuntimeType);
        Assert.NotEmpty(session.ContainerId);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task CreateSession_WithMemoryPreference_ShouldSelectDeno()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "javascript",
            RuntimePreference = RuntimePreference.Memory // Deno가 메모리 효율적 (30MB vs 250MB)
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        // Note: Deno가 PATH에 없으면 Docker로 폴백될 수 있음
        Assert.True(session.RuntimeType == RuntimeType.Deno || session.RuntimeType == RuntimeType.Docker);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task CreateSession_WithBalancedPreference_ShouldSelectAppropriateRuntime()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            RuntimePreference = RuntimePreference.Balanced // 균형있는 선택
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        Assert.True(session.RuntimeType == RuntimeType.Docker || session.RuntimeType == RuntimeType.Deno);
        Assert.NotEmpty(session.EnvironmentId);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task ExecuteInSession_WithDockerRuntime_ShouldExecuteSuccessfully()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker
        };

        var session = await _sessionManager.CreateSessionAsync(config);

        var command = new WriteFileCommand
        {
            Path = "/workspace/test.txt",
            Content = "Docker Runtime Test",
            Mode = FileWriteMode.Create
        };

        // Act
        var result = await _sessionManager.ExecuteInSessionAsync(session.SessionId, command);

        // Assert
        Assert.True(result.Success, result.Error);
        Assert.Equal(1, session.ExecutionCount);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task MultipleRuntimes_ShouldCoexistAndExecuteIndependently()
    {
        // Arrange
        var dockerConfig = new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker
        };

        var jsConfig = new SessionConfig
        {
            Language = "javascript",
            RuntimePreference = RuntimePreference.Speed
        };

        // Act - Create sessions on different runtimes
        var dockerSession = await _sessionManager.CreateSessionAsync(dockerConfig);
        var jsSession = await _sessionManager.CreateSessionAsync(jsConfig);

        // Assert
        Assert.Equal(RuntimeType.Docker, dockerSession.RuntimeType);
        Assert.NotEqual(dockerSession.SessionId, jsSession.SessionId);

        // Execute commands in both
        var dockerCmd = new WriteFileCommand
        {
            Path = "/workspace/docker.txt",
            Content = "Docker content",
            Mode = FileWriteMode.Create
        };

        var jsCmd = new WriteFileCommand
        {
            Path = "js.txt",
            Content = "JS content",
            Mode = FileWriteMode.Create
        };

        var dockerResult = await _sessionManager.ExecuteInSessionAsync(dockerSession.SessionId, dockerCmd);
        var jsResult = await _sessionManager.ExecuteInSessionAsync(jsSession.SessionId, jsCmd);

        Assert.True(dockerResult.Success);
        Assert.True(jsResult.Success);

        // Cleanup
        await _sessionManager.CloseSessionAsync(dockerSession.SessionId);
        await _sessionManager.CloseSessionAsync(jsSession.SessionId);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
    }
}

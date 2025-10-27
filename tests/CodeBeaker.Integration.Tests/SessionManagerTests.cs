using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Session Manager 통합 테스트
/// </summary>
public sealed class SessionManagerTests : IDisposable
{
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        _sessionManager = new SessionManager();
    }

    [Fact]
    public async Task CreateSession_ShouldCreateActiveSession()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            IdleTimeoutMinutes = 30,
            MaxLifetimeMinutes = 120
        };

        // Act
        var session = await _sessionManager.CreateSessionAsync(config);

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.NotEmpty(session.ContainerId);
        Assert.Equal("python", session.Language);
        Assert.Equal(SessionState.Active, session.State);
        Assert.Equal(0, session.ExecutionCount);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task GetSession_ShouldReturnExistingSession()
    {
        // Arrange
        var config = new SessionConfig { Language = "python" };
        var created = await _sessionManager.CreateSessionAsync(config);

        // Act
        var retrieved = await _sessionManager.GetSessionAsync(created.SessionId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(created.SessionId, retrieved.SessionId);
        Assert.Equal(created.ContainerId, retrieved.ContainerId);

        // Cleanup
        await _sessionManager.CloseSessionAsync(created.SessionId);
    }

    [Fact]
    public async Task GetSession_ShouldReturnNull_ForNonExistentSession()
    {
        // Act
        var session = await _sessionManager.GetSessionAsync("non-existent-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task ExecuteInSession_ShouldExecuteCommand()
    {
        // Arrange
        var config = new SessionConfig { Language = "python" };
        var session = await _sessionManager.CreateSessionAsync(config);

        var command = new WriteFileCommand
        {
            Path = "/workspace/test.txt",
            Content = "Hello from session test",
            Mode = FileWriteMode.Create
        };

        // Act
        var result = await _sessionManager.ExecuteInSessionAsync(session.SessionId, command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(1, session.ExecutionCount);
        Assert.Equal(SessionState.Idle, session.State);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task ExecuteInSession_ShouldMaintainFilesystemState()
    {
        // Arrange
        var config = new SessionConfig { Language = "python" };
        var session = await _sessionManager.CreateSessionAsync(config);

        // Act 1: Write file
        var writeCmd = new WriteFileCommand
        {
            Path = "/workspace/persistent.txt",
            Content = "This should persist",
            Mode = FileWriteMode.Create
        };
        var writeResult = await _sessionManager.ExecuteInSessionAsync(session.SessionId, writeCmd);

        // Act 2: Read file (in same session)
        var readCmd = new ReadFileCommand
        {
            Path = "/workspace/persistent.txt"
        };
        var readResult = await _sessionManager.ExecuteInSessionAsync(session.SessionId, readCmd);

        // Assert
        Assert.True(writeResult.Success);
        Assert.True(readResult.Success);
        Assert.Equal(2, session.ExecutionCount);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    [Fact]
    public async Task ExecuteInSession_ShouldThrow_ForClosedSession()
    {
        // Arrange
        var config = new SessionConfig { Language = "python" };
        var session = await _sessionManager.CreateSessionAsync(config);
        await _sessionManager.CloseSessionAsync(session.SessionId);

        var command = new WriteFileCommand
        {
            Path = "/workspace/test.txt",
            Content = "This should fail"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sessionManager.ExecuteInSessionAsync(session.SessionId, command);
        });
    }

    [Fact]
    public async Task ListSessions_ShouldReturnAllActiveSessions()
    {
        // Arrange
        var config1 = new SessionConfig { Language = "python" };
        var config2 = new SessionConfig { Language = "javascript" };

        var session1 = await _sessionManager.CreateSessionAsync(config1);
        var session2 = await _sessionManager.CreateSessionAsync(config2);

        // Act
        var sessions = await _sessionManager.ListSessionsAsync();

        // Assert
        Assert.NotNull(sessions);
        Assert.True(sessions.Count >= 2);
        Assert.Contains(sessions, s => s.SessionId == session1.SessionId);
        Assert.Contains(sessions, s => s.SessionId == session2.SessionId);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session1.SessionId);
        await _sessionManager.CloseSessionAsync(session2.SessionId);
    }

    [Fact]
    public async Task CloseSession_ShouldRemoveSession()
    {
        // Arrange
        var config = new SessionConfig { Language = "python" };
        var session = await _sessionManager.CreateSessionAsync(config);

        // Act
        await _sessionManager.CloseSessionAsync(session.SessionId);

        // Assert
        var retrieved = await _sessionManager.GetSessionAsync(session.SessionId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task CleanupExpiredSessions_ShouldRemoveExpiredSessions()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            IdleTimeoutMinutes = 0, // Expire immediately
            MaxLifetimeMinutes = 120
        };

        var session = await _sessionManager.CreateSessionAsync(config);

        // Wait for expiry
        await Task.Delay(100);

        // Act
        await _sessionManager.CleanupExpiredSessionsAsync();

        // Assert
        var retrieved = await _sessionManager.GetSessionAsync(session.SessionId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task UpdateActivity_ShouldPreventTimeout()
    {
        // Arrange
        var config = new SessionConfig
        {
            Language = "python",
            IdleTimeoutMinutes = 1,
            MaxLifetimeMinutes = 120
        };

        var session = await _sessionManager.CreateSessionAsync(config);
        var command = new WriteFileCommand
        {
            Path = "/workspace/test.txt",
            Content = "Keep alive"
        };

        // Act: Execute command to update activity
        await _sessionManager.ExecuteInSessionAsync(session.SessionId, command);

        // Assert: Session should still exist
        var retrieved = await _sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(retrieved);
        Assert.Equal(SessionState.Idle, retrieved.State);

        // Cleanup
        await _sessionManager.CloseSessionAsync(session.SessionId);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
    }
}

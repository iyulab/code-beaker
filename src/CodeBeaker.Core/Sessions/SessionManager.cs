using System.Collections.Concurrent;
using CodeBeaker.Commands;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Docker;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CodeBeaker.Core.Sessions;

/// <summary>
/// 세션 관리자 (Stateful container 재사용)
/// </summary>
public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionManager()
    {
        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _docker = new DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
        _commandExecutor = new CommandExecutor(_docker);
    }

    /// <summary>
    /// 세션 생성
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sessionId = Guid.NewGuid().ToString("N");

            // Docker 이미지 결정
            var dockerImage = config.DockerImage ?? GetDefaultImage(config.Language);

            // 컨테이너 생성 (장기 실행)
            var createParams = new CreateContainerParameters
            {
                Image = dockerImage,
                Cmd = new[] { "sleep", "infinity" }, // Keep alive
                AttachStdout = true,
                AttachStderr = true,
                Tty = false,
                WorkingDir = "/workspace",
                Labels = new Dictionary<string, string>
                {
                    ["codebeaker.session"] = sessionId,
                    ["codebeaker.language"] = config.Language,
                    ["codebeaker.created"] = DateTime.UtcNow.ToString("o")
                },
                HostConfig = new HostConfig
                {
                    Memory = config.MemoryLimitMB.HasValue ? config.MemoryLimitMB.Value * 1024 * 1024 : 512 * 1024 * 1024,
                    CPUShares = config.CpuShares ?? 1024,
                    NetworkMode = "none",
                    AutoRemove = false // 수동 관리
                }
            };

            var container = await _docker.Containers.CreateContainerAsync(createParams, cancellationToken);
            await _docker.Containers.StartContainerAsync(container.ID, new ContainerStartParameters(), cancellationToken);

            var session = new Session
            {
                SessionId = sessionId,
                ContainerId = container.ID,
                Language = config.Language,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                State = SessionState.Active,
                Config = config
            };

            _sessions[sessionId] = session;

            return session;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 세션 조회
    /// </summary>
    public Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <summary>
    /// 세션에서 명령 실행
    /// </summary>
    public async Task<CommandResult> ExecuteInSessionAsync(
        string sessionId,
        Command command,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session not found: {sessionId}");
        }

        if (session.State == SessionState.Closed || session.State == SessionState.Closing)
        {
            throw new InvalidOperationException($"Session is closed: {sessionId}");
        }

        // 활동 업데이트
        session.UpdateActivity();

        // 컨테이너에서 명령 실행
        var result = await _commandExecutor.ExecuteAsync(
            command,
            session.ContainerId,
            cancellationToken);

        // Idle 상태로 전환
        if (session.State == SessionState.Active)
        {
            session.State = SessionState.Idle;
        }

        return result;
    }

    /// <summary>
    /// 세션 종료
    /// </summary>
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return;
        }

        session.State = SessionState.Closing;

        try
        {
            // 컨테이너 정지 및 삭제
            await _docker.Containers.StopContainerAsync(
                session.ContainerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                cancellationToken);

            await _docker.Containers.RemoveContainerAsync(
                session.ContainerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);

            session.State = SessionState.Closed;
        }
        catch
        {
            // 컨테이너가 이미 없을 수 있음
            session.State = SessionState.Closed;
        }
    }

    /// <summary>
    /// 모든 세션 목록
    /// </summary>
    public Task<List<Session>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values.ToList();
        return Task.FromResult(sessions);
    }

    /// <summary>
    /// 만료된 세션 정리
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredSessions = _sessions.Values
            .Where(s => s.IsExpired(now) && s.State != SessionState.Closed && s.State != SessionState.Closing)
            .ToList();

        foreach (var session in expiredSessions)
        {
            await CloseSessionAsync(session.SessionId, cancellationToken);
        }
    }

    private static string GetDefaultImage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "python" => "codebeaker-python:latest",
            "javascript" => "codebeaker-nodejs:latest",
            "go" => "codebeaker-golang:latest",
            "csharp" => "codebeaker-dotnet:latest",
            _ => throw new ArgumentException($"Unknown language: {language}")
        };
    }

    public void Dispose()
    {
        _lock.Dispose();
        _docker.Dispose();
    }
}

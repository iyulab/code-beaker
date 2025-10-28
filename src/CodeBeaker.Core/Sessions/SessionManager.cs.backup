using System.Collections.Concurrent;
using CodeBeaker.Commands;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Docker;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Runtime;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CodeBeaker.Core.Sessions;

/// <summary>
/// 세션 관리자 (Multi-Runtime 지원)
/// </summary>
public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly RuntimeSelector _runtimeSelector;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionManager(IEnumerable<IExecutionRuntime> runtimes)
    {
        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _docker = new DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
        _commandExecutor = new CommandExecutor(_docker);
        _runtimeSelector = new RuntimeSelector(runtimes);
    }

    /// <summary>
    /// 세션 생성 (Multi-Runtime 지원)
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sessionId = Guid.NewGuid().ToString("N");

            // 1. 런타임 선택
            IExecutionRuntime runtime;
            if (config.RuntimeType.HasValue)
            {
                // 특정 런타임 강제 지정
                runtime = await _runtimeSelector.SelectByTypeAsync(
                    config.RuntimeType.Value,
                    config.Language,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Runtime {config.RuntimeType.Value} not available for {config.Language}");
            }
            else
            {
                // 자동 선택 (Preference 기반)
                runtime = await _runtimeSelector.SelectBestRuntimeAsync(
                    config.Language,
                    config.RuntimePreference,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"No runtime available for language: {config.Language}");
            }

            // 2. RuntimeConfig 생성
            var runtimeConfig = new RuntimeConfig
            {
                Environment = config.Language,
                WorkspaceDirectory = Path.Combine(
                    Path.GetTempPath(),
                    $"codebeaker-{sessionId}"),
                ResourceLimits = new ResourceLimits
                {
                    MemoryLimitMB = config.MemoryLimitMB,
                    CpuShares = config.CpuShares,
                    TimeoutSeconds = 300
                },
                Permissions = new PermissionSettings
                {
                    AllowRead = new List<string> { "/workspace", "/tmp" },
                    AllowWrite = new List<string> { "/workspace", "/tmp" },
                    AllowNet = false,
                    AllowEnv = false
                }
            };

            // 3. 실행 환경 생성
            var environment = await runtime.CreateEnvironmentAsync(runtimeConfig, cancellationToken);

            // 4. Session 생성
            var session = new Session
            {
                SessionId = sessionId,
                EnvironmentId = environment.EnvironmentId,
                RuntimeType = runtime.Type,
                Environment = environment,
                Language = config.Language,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                State = SessionState.Active,
                Config = config
            };

            // Docker Runtime이면 ContainerId도 설정
            if (runtime.Type == Interfaces.RuntimeType.Docker)
            {
                session.ContainerId = environment.EnvironmentId;
            }

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
    /// 세션에서 명령 실행 (Multi-Runtime)
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

        if (session.Environment == null)
        {
            throw new InvalidOperationException($"Session environment is null: {sessionId}");
        }

        // 활동 업데이트
        session.UpdateActivity();

        CommandResult result;

        // 모든 Runtime은 IExecutionEnvironment.ExecuteAsync 사용
        result = await session.Environment.ExecuteAsync(command, cancellationToken);

        // Idle 상태로 전환
        if (session.State == SessionState.Active)
        {
            session.State = SessionState.Idle;
        }

        return result;
    }

    /// <summary>
    /// 세션 종료 (Multi-Runtime)
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
            if (session.RuntimeType == Interfaces.RuntimeType.Docker)
            {
                // Docker: 기존 방식으로 컨테이너 정리
                await _docker.Containers.StopContainerAsync(
                    session.ContainerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                    cancellationToken);

                await _docker.Containers.RemoveContainerAsync(
                    session.ContainerId,
                    new ContainerRemoveParameters { Force = true },
                    cancellationToken);
            }
            else
            {
                // 다른 Runtime: IExecutionEnvironment.DisposeAsync 사용
                if (session.Environment != null)
                {
                    await session.Environment.DisposeAsync();
                }
            }

            session.State = SessionState.Closed;
        }
        catch
        {
            // 환경이 이미 정리되었을 수 있음
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
            "javascript" or "js" or "nodejs" => "codebeaker-nodejs:latest",
            "go" or "golang" => "codebeaker-golang:latest",
            "csharp" or "cs" or "dotnet" => "codebeaker-dotnet:latest",
            _ => throw new NotSupportedException($"Language not supported: {language}")
        };
    }

    public void Dispose()
    {
        _docker?.Dispose();
        _lock?.Dispose();
    }
}

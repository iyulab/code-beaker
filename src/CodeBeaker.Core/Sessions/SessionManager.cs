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
/// 세션 관리자 (Multi-Runtime 지원 + 분산 스토리지)
/// </summary>
public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly ISessionStore _sessionStore;
    private readonly ConcurrentDictionary<string, IExecutionEnvironment> _activeEnvironments = new();
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly RuntimeSelector _runtimeSelector;

    public SessionManager(
        ISessionStore sessionStore,
        IEnumerable<IExecutionRuntime> runtimes)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));

        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _docker = new DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
        _commandExecutor = new CommandExecutor(_docker);
        _runtimeSelector = new RuntimeSelector(runtimes);
    }

    /// <summary>
    /// 세션 생성 (Multi-Runtime 지원 + 분산 락)
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        // 분산 락 획득
        await using var lockHandle = await _sessionStore.AcquireLockAsync(
            $"create:{sessionId}",
            TimeSpan.FromSeconds(10),
            cancellationToken);

        if (lockHandle == null)
        {
            throw new InvalidOperationException("Failed to acquire lock for session creation");
        }

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

        // 환경 캐싱 (메모리에만 유지)
        _activeEnvironments[sessionId] = environment;

        // 세션 데이터 저장 (분산 스토리지)
        var sessionData = SessionMapper.ToSessionData(session);
        await _sessionStore.SaveSessionAsync(sessionData, cancellationToken);

        return session;
    }

    /// <summary>
    /// 세션 조회 (분산 스토리지 + Environment 재구성)
    /// </summary>
    public async Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // 1. 스토리지에서 세션 데이터 조회
        var sessionData = await _sessionStore.GetSessionAsync(sessionId, cancellationToken);
        if (sessionData == null)
        {
            return null;
        }

        // 2. SessionData → Session 변환
        var session = SessionMapper.FromSessionData(sessionData);

        // 3. Environment 재구성 (캐시 확인 후)
        if (!_activeEnvironments.TryGetValue(sessionId, out var environment))
        {
            // 캐시에 없으면 재구성 (다른 API 인스턴스에서 생성된 경우)
            environment = await ReconstructEnvironmentAsync(session, cancellationToken);
            if (environment != null)
            {
                _activeEnvironments[sessionId] = environment;
            }
        }

        session.Environment = environment;
        return session;
    }

    /// <summary>
    /// Environment 재구성 (다른 API 인스턴스에서 생성된 세션용)
    /// </summary>
    private async Task<IExecutionEnvironment?> ReconstructEnvironmentAsync(
        Session session,
        CancellationToken cancellationToken)
    {
        try
        {
            // Docker Runtime의 경우 기존 컨테이너 연결
            if (session.RuntimeType == Interfaces.RuntimeType.Docker)
            {
                // ContainerId를 사용하여 Docker Environment 재구성
                // 실제 구현은 DockerRuntime에 ReconnectEnvironmentAsync 메서드 필요
                // 현재는 간단히 null 반환 (TODO: 구현 필요)
                return null;
            }

            // 다른 런타임은 현재 재구성 불가 (프로세스 기반이므로)
            return null;
        }
        catch
        {
            // 재구성 실패 시 null 반환
            return null;
        }
    }

    /// <summary>
    /// 세션에서 명령 실행 (Multi-Runtime + 분산 스토리지)
    /// </summary>
    public async Task<CommandResult> ExecuteInSessionAsync(
        string sessionId,
        Command command,
        CancellationToken cancellationToken = default)
    {
        // 1. 세션 조회 (분산 스토리지)
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
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

        // 2. 활동 업데이트 (분산 스토리지)
        await _sessionStore.UpdateActivityAsync(sessionId, cancellationToken);

        // 3. 명령 실행
        var result = await session.Environment.ExecuteAsync(command, cancellationToken);

        // 4. Idle 상태로 전환 후 저장
        if (session.State == SessionState.Active)
        {
            session.State = SessionState.Idle;
            var sessionData = SessionMapper.ToSessionData(session);
            await _sessionStore.SaveSessionAsync(sessionData, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// 세션 종료 (Multi-Runtime + 분산 스토리지)
    /// </summary>
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // 1. 세션 조회
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return;
        }

        session.State = SessionState.Closing;

        try
        {
            if (session.RuntimeType == Interfaces.RuntimeType.Docker)
            {
                // Docker: 컨테이너 정리
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
        finally
        {
            // 2. 환경 캐시 제거
            _activeEnvironments.TryRemove(sessionId, out _);

            // 3. 스토리지에서 삭제
            await _sessionStore.RemoveSessionAsync(sessionId, cancellationToken);
        }
    }

    /// <summary>
    /// 모든 세션 목록 (분산 스토리지)
    /// </summary>
    public async Task<List<Session>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        // 1. 스토리지에서 모든 세션 데이터 조회
        var sessionDataList = await _sessionStore.ListSessionsAsync(cancellationToken);

        // 2. SessionData → Session 변환
        var sessions = new List<Session>();
        foreach (var sessionData in sessionDataList)
        {
            var session = SessionMapper.FromSessionData(sessionData);

            // Environment는 캐시에서 가져오기 (있으면)
            if (_activeEnvironments.TryGetValue(session.SessionId, out var environment))
            {
                session.Environment = environment;
            }

            sessions.Add(session);
        }

        return sessions;
    }

    /// <summary>
    /// 만료된 세션 정리 (분산 스토리지)
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // 1. 모든 세션 조회
        var sessionDataList = await _sessionStore.ListSessionsAsync(cancellationToken);

        // 2. 만료된 세션 필터링
        var expiredSessions = sessionDataList
            .Where(s => s.IsExpired(now) && s.State != "Closed" && s.State != "Closing")
            .ToList();

        // 3. 만료된 세션 종료
        foreach (var sessionData in expiredSessions)
        {
            await CloseSessionAsync(sessionData.SessionId, cancellationToken);
        }
    }

    /// <summary>
    /// 세션의 리소스 사용량 조회 (Phase 8.1)
    /// IResourceMonitor를 구현한 런타임만 지원
    /// </summary>
    public async Task<ResourceUsage?> GetSessionResourceUsageAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // 1. 활성 Environment 조회
        if (!_activeEnvironments.TryGetValue(sessionId, out var environment))
        {
            // 환경이 캐시에 없으면 세션을 조회해서 재구성 시도
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session?.Environment == null)
            {
                return null;
            }
            environment = session.Environment;
        }

        // 2. IResourceMonitor 구현 확인
        if (environment is not IResourceMonitor monitor)
        {
            // 리소스 모니터링을 지원하지 않는 런타임
            return null;
        }

        // 3. 리소스 사용량 조회
        try
        {
            return await monitor.GetCurrentUsageAsync(cancellationToken);
        }
        catch
        {
            // 리소스 조회 실패 시 null 반환
            return null;
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
    }
}

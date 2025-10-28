namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 세션 스토리지 추상화 인터페이스
/// 다양한 저장소 구현 지원 (InMemory, Redis, 등)
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// 세션 저장
    /// </summary>
    Task SaveSessionAsync(SessionData session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 조회
    /// </summary>
    Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 삭제
    /// </summary>
    Task<bool> RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 모든 세션 목록 조회
    /// </summary>
    Task<List<SessionData>> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 존재 여부 확인
    /// </summary>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 활동 시각 업데이트
    /// </summary>
    Task UpdateActivityAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 분산 락 획득 (Redis 등에서 사용)
    /// </summary>
    Task<IAsyncDisposable?> AcquireLockAsync(
        string lockKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 직렬화 가능한 세션 데이터 (IExecutionEnvironment 제외)
/// </summary>
public sealed class SessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public RuntimeType RuntimeType { get; set; }
    public string Language { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public string State { get; set; } = string.Empty;
    public SessionConfigData Config { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public int ExecutionCount { get; set; }

    /// <summary>
    /// 세션이 만료되었는지 확인
    /// </summary>
    public bool IsExpired(DateTime now)
    {
        var idleTime = now - LastActivity;
        var lifetime = now - CreatedAt;

        return idleTime.TotalMinutes > Config.IdleTimeoutMinutes ||
               lifetime.TotalMinutes > Config.MaxLifetimeMinutes;
    }
}

/// <summary>
/// 직렬화 가능한 세션 설정 데이터
/// </summary>
public sealed class SessionConfigData
{
    public string Language { get; set; } = string.Empty;
    public string? RuntimePreference { get; set; }
    public string? RuntimeType { get; set; }
    public string? DockerImage { get; set; }
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int MaxLifetimeMinutes { get; set; } = 120;
    public bool PersistFilesystem { get; set; } = true;
    public long? MemoryLimitMB { get; set; }
    public long? CpuShares { get; set; }
}

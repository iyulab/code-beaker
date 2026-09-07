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
    /// 세션 활동 시각 업데이트.
    ///
    /// 이름 그대로 <c>LastActivity</c> 만 갱신한다 — 실행 횟수나 세션 상태는 저장소가 아니라
    /// 호출자(<c>SessionManager</c>)가 소유한다. 저장소가 함께 바꾸면 호출자가 뒤이어 저장하는
    /// 스냅샷에 조용히 덮어써져, 두 곳이 같은 필드를 쓰면서 한쪽만 이기는 상태가 된다.
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

    /// <summary>
    /// Runtime-issued environment identifier — see
    /// <see cref="Models.Session.EnvironmentId"/> for the contract.
    /// </summary>
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

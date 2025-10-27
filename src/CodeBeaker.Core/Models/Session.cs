namespace CodeBeaker.Core.Models;

/// <summary>
/// 실행 세션 (Stateful container)
/// </summary>
public sealed class Session
{
    /// <summary>
    /// 세션 ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 컨테이너 ID
    /// </summary>
    public string ContainerId { get; set; } = string.Empty;

    /// <summary>
    /// 언어
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 생성 시각
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 마지막 활동 시각
    /// </summary>
    public DateTime LastActivity { get; set; }

    /// <summary>
    /// 세션 상태
    /// </summary>
    public SessionState State { get; set; }

    /// <summary>
    /// 세션 설정
    /// </summary>
    public SessionConfig Config { get; set; } = new();

    /// <summary>
    /// 메타데이터
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 실행 횟수
    /// </summary>
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

    /// <summary>
    /// 활동 시각 업데이트
    /// </summary>
    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
        ExecutionCount++;

        if (State == SessionState.Idle)
        {
            State = SessionState.Active;
        }
    }
}

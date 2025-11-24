namespace CodeBeaker.Core.Models;

/// <summary>
/// 세션 상태
/// </summary>
public enum SessionState
{
    /// <summary>
    /// 생성 중
    /// </summary>
    Creating,

    /// <summary>
    /// 활성 (실행 중)
    /// </summary>
    Active,

    /// <summary>
    /// 유휴 상태 (타임아웃 대기)
    /// </summary>
    Idle,

    /// <summary>
    /// 종료 중
    /// </summary>
    Closing,

    /// <summary>
    /// 종료됨
    /// </summary>
    Closed
}

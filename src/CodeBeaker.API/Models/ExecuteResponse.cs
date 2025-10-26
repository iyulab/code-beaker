namespace CodeBeaker.API.Models;

/// <summary>
/// 코드 실행 응답
/// </summary>
public sealed class ExecuteResponse
{
    /// <summary>
    /// 실행 ID
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// 실행 상태 (pending, running, completed, failed, timeout)
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

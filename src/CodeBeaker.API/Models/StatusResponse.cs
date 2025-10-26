using CodeBeaker.Core.Models;

namespace CodeBeaker.API.Models;

/// <summary>
/// 실행 상태 조회 응답
/// </summary>
public sealed class StatusResponse
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
    /// 종료 코드 (완료된 경우)
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// 표준 출력 (완료된 경우)
    /// </summary>
    public string? Stdout { get; set; }

    /// <summary>
    /// 표준 에러 (완료된 경우)
    /// </summary>
    public string? Stderr { get; set; }

    /// <summary>
    /// 실행 시간 (밀리초)
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 타임아웃 여부
    /// </summary>
    public bool? Timeout { get; set; }

    /// <summary>
    /// 에러 타입
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 완료 시간
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    public static StatusResponse FromExecutionResult(ExecutionResult result)
    {
        return new StatusResponse
        {
            ExecutionId = result.ExecutionId,
            Status = result.Status,
            ExitCode = result.ExitCode,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            DurationMs = result.DurationMs,
            Timeout = result.Timeout,
            ErrorType = result.ErrorType,
            CreatedAt = result.CreatedAt,
            CompletedAt = result.CompletedAt
        };
    }
}

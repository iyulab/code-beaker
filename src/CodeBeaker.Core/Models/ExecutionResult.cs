namespace CodeBeaker.Core.Models;

/// <summary>
/// 코드 실행 결과
/// </summary>
public sealed class ExecutionResult
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
    /// 종료 코드
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 표준 출력
    /// </summary>
    public string Stdout { get; set; } = string.Empty;

    /// <summary>
    /// 표준 에러
    /// </summary>
    public string Stderr { get; set; } = string.Empty;

    /// <summary>
    /// 실행 시간 (밀리초)
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 타임아웃 발생 여부
    /// </summary>
    public bool Timeout { get; set; }

    /// <summary>
    /// 에러 타입 (syntax_error, runtime_error, timeout_error 등)
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 완료 시간
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

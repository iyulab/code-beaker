namespace CodeBeaker.API.Models;

/// <summary>
/// API 에러 응답
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// 에러 메시지
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 에러 코드
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 상세 정보 (개발 환경에서만)
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 타임스탬프
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

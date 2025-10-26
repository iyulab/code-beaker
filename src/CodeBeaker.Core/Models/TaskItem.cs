namespace CodeBeaker.Core.Models;

/// <summary>
/// 작업 큐 아이템
/// </summary>
public sealed class TaskItem
{
    /// <summary>
    /// 실행 ID
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// 실행할 코드
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 프로그래밍 언어 (python, javascript, go, csharp)
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 실행 설정
    /// </summary>
    public ExecutionConfig Config { get; set; } = new();

    /// <summary>
    /// 작업 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 작업 파일명 (큐 관리용)
    /// </summary>
    public string? FileName { get; set; }
}

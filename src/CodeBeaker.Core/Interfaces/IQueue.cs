using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 작업 큐 인터페이스
/// </summary>
public interface IQueue
{
    /// <summary>
    /// 작업을 큐에 추가
    /// </summary>
    /// <param name="code">실행할 코드</param>
    /// <param name="language">프로그래밍 언어</param>
    /// <param name="config">실행 설정</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 ID</returns>
    Task<string> SubmitTaskAsync(
        string code,
        string language,
        ExecutionConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 큐에서 작업을 가져옴 (FIFO)
    /// </summary>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>작업 아이템 또는 null</returns>
    Task<TaskItem?> GetTaskAsync(
        int timeout = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 작업 완료 처리
    /// </summary>
    /// <param name="executionId">실행 ID</param>
    /// <param name="cancellationToken">취소 토큰</param>
    Task CompleteTaskAsync(
        string executionId,
        CancellationToken cancellationToken = default);
}

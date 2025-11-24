using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 실행 결과 저장소 인터페이스
/// </summary>
public interface IStorage
{
    /// <summary>
    /// 실행 상태 업데이트
    /// </summary>
    /// <param name="executionId">실행 ID</param>
    /// <param name="status">상태</param>
    /// <param name="exitCode">종료 코드</param>
    /// <param name="durationMs">실행 시간 (밀리초)</param>
    /// <param name="timeout">타임아웃 여부</param>
    /// <param name="errorType">에러 타입</param>
    /// <param name="cancellationToken">취소 토큰</param>
    Task UpdateStatusAsync(
        string executionId,
        string status,
        int exitCode = 0,
        long durationMs = 0,
        bool timeout = false,
        string? errorType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 실행 결과 저장
    /// </summary>
    /// <param name="executionId">실행 ID</param>
    /// <param name="stdout">표준 출력</param>
    /// <param name="stderr">표준 에러</param>
    /// <param name="exitCode">종료 코드</param>
    /// <param name="durationMs">실행 시간 (밀리초)</param>
    /// <param name="timeout">타임아웃 여부</param>
    /// <param name="errorType">에러 타입</param>
    /// <param name="cancellationToken">취소 토큰</param>
    Task SaveResultAsync(
        string executionId,
        string stdout,
        string stderr,
        int exitCode,
        long durationMs,
        bool timeout = false,
        string? errorType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 실행 결과 조회
    /// </summary>
    /// <param name="executionId">실행 ID</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 결과 또는 null</returns>
    Task<ExecutionResult?> GetResultAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 실행 상태 조회
    /// </summary>
    /// <param name="executionId">실행 ID</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 결과 (상태만 포함) 또는 null</returns>
    Task<ExecutionResult?> GetStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default);
}

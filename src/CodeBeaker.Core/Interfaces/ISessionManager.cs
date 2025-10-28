using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 세션 관리자 인터페이스
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 세션 생성
    /// </summary>
    Task<Session> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 조회
    /// </summary>
    Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션에서 명령 실행
    /// </summary>
    Task<CommandResult> ExecuteInSessionAsync(
        string sessionId,
        Command command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션 종료
    /// </summary>
    Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 모든 세션 목록 조회
    /// </summary>
    Task<List<Session>> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 만료된 세션 정리
    /// </summary>
    Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 세션의 리소스 사용량 조회 (Phase 8.1)
    /// IResourceMonitor를 구현한 런타임만 지원
    /// </summary>
    Task<ResourceUsage?> GetSessionResourceUsageAsync(string sessionId, CancellationToken cancellationToken = default);
}

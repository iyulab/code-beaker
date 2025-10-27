using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 언어별 런타임 인터페이스
/// </summary>
public interface IRuntime
{
    /// <summary>
    /// 지원하는 언어 이름
    /// </summary>
    string LanguageName { get; }

    /// <summary>
    /// Docker 이미지 이름
    /// </summary>
    string DockerImage { get; }

    /// <summary>
    /// 코드 실행
    /// </summary>
    /// <param name="code">실행할 코드</param>
    /// <param name="config">실행 설정</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 결과</returns>
    Task<ExecutionResult> ExecuteAsync(
        string code,
        ExecutionConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 실행 명령어 가져오기 (Legacy - 하위 호환성)
    /// </summary>
    /// <param name="entryPoint">엔트리 포인트 파일명</param>
    /// <param name="packages">추가 패키지 목록</param>
    /// <returns>실행 명령어 배열</returns>
    string[] GetRunCommand(string entryPoint, List<string>? packages = null);

    /// <summary>
    /// 실행 계획 가져오기 (Command 기반 - Phase 2)
    /// </summary>
    /// <param name="code">소스 코드</param>
    /// <param name="packages">추가 패키지 목록</param>
    /// <returns>실행할 Command 리스트</returns>
    List<Command> GetExecutionPlan(string code, List<string>? packages = null);

    /// <summary>
    /// 작업 디렉토리 설정
    /// </summary>
    /// <param name="code">소스 코드</param>
    /// <param name="workspaceDir">작업 디렉토리 경로</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>엔트리 포인트 파일명</returns>
    Task<string> SetupWorkspaceAsync(
        string code,
        string workspaceDir,
        CancellationToken cancellationToken = default);
}

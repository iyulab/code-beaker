using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Docker;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// 모든 언어 런타임의 기본 클래스
/// </summary>
public abstract class BaseRuntime : IRuntime
{
    protected readonly DockerExecutor _executor;

    protected BaseRuntime()
    {
        _executor = new DockerExecutor();
    }

    /// <summary>
    /// 언어 이름 (python, javascript, go, csharp)
    /// </summary>
    public abstract string LanguageName { get; }

    /// <summary>
    /// Docker 이미지 이름
    /// </summary>
    public abstract string DockerImage { get; }

    /// <summary>
    /// 파일 확장자 (.py, .js, .go, .cs)
    /// </summary>
    protected abstract string FileExtension { get; }

    /// <summary>
    /// 코드 실행
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        string code,
        ExecutionConfig config,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();
        var workspaceDir = Path.Combine(Path.GetTempPath(), $"codebeaker_{executionId}");

        try
        {
            // 1. Setup workspace
            var entryPoint = await SetupWorkspaceAsync(code, workspaceDir, cancellationToken);

            // 2. Get run command
            var command = GetRunCommand(entryPoint, config.Packages);

            // 3. Execute via Docker
            var result = await _executor.ExecuteAsync(
                DockerImage,
                command,
                workspaceDir,
                config,
                cancellationToken);

            result.ExecutionId = executionId;
            return result;
        }
        finally
        {
            // 4. Cleanup workspace
            try
            {
                if (Directory.Exists(workspaceDir))
                {
                    Directory.Delete(workspaceDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// 작업 디렉토리 설정
    /// </summary>
    public virtual async Task<string> SetupWorkspaceAsync(
        string code,
        string workspaceDir,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(workspaceDir);

        var fileName = $"main{FileExtension}";
        var filePath = Path.Combine(workspaceDir, fileName);

        await File.WriteAllTextAsync(filePath, code, cancellationToken);

        return fileName;
    }

    /// <summary>
    /// 실행 명령어 가져오기 (Legacy - 언어별 구현)
    /// </summary>
    public abstract string[] GetRunCommand(string entryPoint, List<string>? packages = null);

    /// <summary>
    /// 실행 계획 가져오기 (Command 기반 - Phase 2, 언어별 구현)
    /// </summary>
    public abstract List<Command> GetExecutionPlan(string code, List<string>? packages = null);
}

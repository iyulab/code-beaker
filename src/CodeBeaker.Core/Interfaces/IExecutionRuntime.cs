using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Core.Interfaces;

/// <summary>
/// 실행 환경 추상화 인터페이스
/// Docker, Deno, Bun 등 다양한 실행 환경을 지원
/// </summary>
public interface IExecutionRuntime
{
    /// <summary>
    /// 런타임 이름 (예: "docker", "deno", "bun")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 런타임 타입
    /// </summary>
    RuntimeType Type { get; }

    /// <summary>
    /// 지원하는 개발환경 (예: "python", "nodejs", "deno")
    /// </summary>
    string[] SupportedEnvironments { get; }

    /// <summary>
    /// 런타임 사용 가능 여부 확인
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 실행 환경 생성 (컨테이너, 프로세스 등)
    /// </summary>
    /// <param name="config">런타임 설정</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 환경 인스턴스</returns>
    Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 런타임 성능 특성
    /// </summary>
    RuntimeCapabilities GetCapabilities();
}

/// <summary>
/// 실행 환경 인스턴스 (컨테이너, 프로세스 등)
/// </summary>
public interface IExecutionEnvironment : IAsyncDisposable
{
    /// <summary>
    /// 환경 고유 ID (컨테이너 ID, 프로세스 ID 등)
    /// </summary>
    string EnvironmentId { get; }

    /// <summary>
    /// 런타임 타입
    /// </summary>
    RuntimeType RuntimeType { get; }

    /// <summary>
    /// 환경 상태
    /// </summary>
    EnvironmentState State { get; }

    /// <summary>
    /// 명령 실행
    /// </summary>
    /// <param name="command">실행할 명령</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 결과</returns>
    Task<CommandResult> ExecuteAsync(
        Command command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 환경 상태 조회
    /// </summary>
    Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 환경 정리
    /// </summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 런타임 타입
/// </summary>
public enum RuntimeType
{
    /// <summary>
    /// Docker 컨테이너 (강력한 격리)
    /// </summary>
    Docker,

    /// <summary>
    /// Deno 런타임 (JavaScript/TypeScript)
    /// </summary>
    Deno,

    /// <summary>
    /// Bun 런타임 (JavaScript/TypeScript)
    /// </summary>
    Bun,

    /// <summary>
    /// Node.js 런타임
    /// </summary>
    NodeJs,

    /// <summary>
    /// WebAssembly (Wasmer)
    /// </summary>
    WebAssembly,

    /// <summary>
    /// V8 Isolates (극경량)
    /// </summary>
    V8Isolate,

    /// <summary>
    /// Native 프로세스 (개발용)
    /// </summary>
    NativeProcess
}

/// <summary>
/// 환경 상태
/// </summary>
public enum EnvironmentState
{
    /// <summary>
    /// 초기화 중
    /// </summary>
    Initializing,

    /// <summary>
    /// 실행 준비 완료
    /// </summary>
    Ready,

    /// <summary>
    /// 실행 중
    /// </summary>
    Running,

    /// <summary>
    /// 유휴 상태
    /// </summary>
    Idle,

    /// <summary>
    /// 종료됨
    /// </summary>
    Stopped,

    /// <summary>
    /// 오류 상태
    /// </summary>
    Error
}

/// <summary>
/// 런타임 성능 특성
/// </summary>
public sealed class RuntimeCapabilities
{
    /// <summary>
    /// 예상 시작 시간 (밀리초)
    /// </summary>
    public int StartupTimeMs { get; set; }

    /// <summary>
    /// 메모리 오버헤드 (MB)
    /// </summary>
    public int MemoryOverheadMB { get; set; }

    /// <summary>
    /// 격리 수준 (0-10, 높을수록 강함)
    /// </summary>
    public int IsolationLevel { get; set; }

    /// <summary>
    /// 파일시스템 영속성 지원
    /// </summary>
    public bool SupportsFilesystemPersistence { get; set; }

    /// <summary>
    /// 네트워크 접근 지원
    /// </summary>
    public bool SupportsNetworkAccess { get; set; }

    /// <summary>
    /// 동시 실행 가능 수
    /// </summary>
    public int MaxConcurrentExecutions { get; set; }
}

/// <summary>
/// 런타임 설정
/// </summary>
public sealed class RuntimeConfig
{
    /// <summary>
    /// 개발환경 (python, nodejs, deno 등)
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// 작업 디렉토리
    /// </summary>
    public string WorkspaceDirectory { get; set; } = "/workspace";

    /// <summary>
    /// 환경 변수
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// 리소스 제한
    /// </summary>
    public ResourceLimits? ResourceLimits { get; set; }

    /// <summary>
    /// 권한 설정
    /// </summary>
    public PermissionSettings? Permissions { get; set; }
}

/// <summary>
/// 리소스 제한
/// </summary>
public sealed class ResourceLimits
{
    /// <summary>
    /// 메모리 제한 (MB)
    /// </summary>
    public long? MemoryLimitMB { get; set; }

    /// <summary>
    /// CPU 제한 (shares)
    /// </summary>
    public long? CpuShares { get; set; }

    /// <summary>
    /// 실행 시간 제한 (초)
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// 권한 설정 (Deno 등에서 사용)
/// </summary>
public sealed class PermissionSettings
{
    /// <summary>
    /// 읽기 권한 경로
    /// </summary>
    public List<string> AllowRead { get; set; } = new();

    /// <summary>
    /// 쓰기 권한 경로
    /// </summary>
    public List<string> AllowWrite { get; set; } = new();

    /// <summary>
    /// 네트워크 접근 허용
    /// </summary>
    public bool AllowNet { get; set; } = false;

    /// <summary>
    /// 환경 변수 접근 허용
    /// </summary>
    public bool AllowEnv { get; set; } = false;

    /// <summary>
    /// 시스템 명령 실행 허용
    /// </summary>
    public bool AllowRun { get; set; } = false;
}

namespace CodeBeaker.Core.Models;

/// <summary>
/// 코드 실행 설정
/// </summary>
public sealed class ExecutionConfig
{
    /// <summary>
    /// 실행 타임아웃 (초)
    /// </summary>
    public int Timeout { get; set; } = 5;

    /// <summary>
    /// 메모리 제한 (MB)
    /// </summary>
    public int MemoryLimit { get; set; } = 256;

    /// <summary>
    /// CPU 제한 (코어 수)
    /// </summary>
    public double CpuLimit { get; set; } = 0.5;

    /// <summary>
    /// 네트워크 비활성화 여부
    /// </summary>
    public bool DisableNetwork { get; set; } = true;

    /// <summary>
    /// 파일 시스템 읽기 전용 여부
    /// </summary>
    public bool ReadOnlyFilesystem { get; set; } = true;

    /// <summary>
    /// 환경 변수
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// 추가 패키지 (언어별 패키지 매니저 사용)
    /// </summary>
    public List<string> Packages { get; set; } = new();
}

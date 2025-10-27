namespace CodeBeaker.Core.Models;

/// <summary>
/// 세션 설정
/// </summary>
public sealed class SessionConfig
{
    /// <summary>
    /// 언어 (python, javascript, go, csharp)
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Docker 이미지
    /// </summary>
    public string? DockerImage { get; set; }

    /// <summary>
    /// 유휴 타임아웃 (분)
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// 최대 생명주기 (분)
    /// </summary>
    public int MaxLifetimeMinutes { get; set; } = 120;

    /// <summary>
    /// 파일시스템 영속화 여부
    /// </summary>
    public bool PersistFilesystem { get; set; } = true;

    /// <summary>
    /// 메모리 제한 (MB)
    /// </summary>
    public long? MemoryLimitMB { get; set; }

    /// <summary>
    /// CPU 제한 (shares)
    /// </summary>
    public long? CpuShares { get; set; }
}

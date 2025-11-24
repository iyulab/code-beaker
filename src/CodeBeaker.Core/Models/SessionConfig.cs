using System.Text.Json.Serialization;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Runtime;

namespace CodeBeaker.Core.Models;

/// <summary>
/// 세션 설정
/// </summary>
public sealed class SessionConfig
{
    /// <summary>
    /// 언어 (python, javascript, go, csharp, deno, typescript)
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 런타임 선택 기준 (Speed, Security, Memory, Balanced)
    /// null이면 자동 선택 (Balanced)
    /// </summary>
    [JsonPropertyName("runtimePreference")]
    public RuntimePreference? RuntimePreference { get; set; }

    /// <summary>
    /// 특정 런타임 타입 강제 지정 (선택적)
    /// null이면 RuntimePreference에 따라 자동 선택
    /// </summary>
    [JsonPropertyName("runtimeType")]
    public RuntimeType? RuntimeType { get; set; }

    /// <summary>
    /// Docker 이미지
    /// </summary>
    [JsonPropertyName("dockerImage")]
    public string? DockerImage { get; set; }

    /// <summary>
    /// 유휴 타임아웃 (분)
    /// </summary>
    [JsonPropertyName("idleTimeoutMinutes")]
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// 최대 생명주기 (분)
    /// </summary>
    [JsonPropertyName("maxLifetimeMinutes")]
    public int MaxLifetimeMinutes { get; set; } = 120;

    /// <summary>
    /// 파일시스템 영속화 여부
    /// </summary>
    [JsonPropertyName("persistFilesystem")]
    public bool PersistFilesystem { get; set; } = true;

    /// <summary>
    /// 메모리 제한 (MB)
    /// </summary>
    [JsonPropertyName("memoryLimitMB")]
    public long? MemoryLimitMB { get; set; }

    /// <summary>
    /// CPU 제한 (shares)
    /// </summary>
    [JsonPropertyName("cpuShares")]
    public long? CpuShares { get; set; }

    /// <summary>
    /// Security configuration
    /// Phase 11: Production Hardening
    /// </summary>
    [JsonPropertyName("security")]
    public SecurityConfig Security { get; set; } = new();
}

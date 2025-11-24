namespace CodeBeaker.API.Models;

/// <summary>
/// 지원 언어 정보
/// </summary>
public sealed class LanguageInfo
{
    /// <summary>
    /// 언어 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 버전
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 별칭 목록
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    /// Docker 이미지
    /// </summary>
    public string DockerImage { get; set; } = string.Empty;
}

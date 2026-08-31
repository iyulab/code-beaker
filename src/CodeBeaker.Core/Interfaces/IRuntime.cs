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
}

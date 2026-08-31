using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Runtimes;

/// <summary>
/// 모든 언어 런타임의 기본 클래스
/// </summary>
public abstract class BaseRuntime : IRuntime
{
    /// <summary>
    /// 언어 이름 (python, javascript, go, csharp)
    /// </summary>
    public abstract string LanguageName { get; }

    /// <summary>
    /// Docker 이미지 이름
    /// </summary>
    public abstract string DockerImage { get; }
}

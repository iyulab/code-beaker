namespace CodeBeaker.Core.Models;

/// <summary>
/// 지원 언어 하나의 메타데이터. 실행 방법을 기술하지 않는다 — 실행은
/// <see cref="Interfaces.IExecutionRuntime"/>이 맡고, 이 레코드는 이름 정규화와
/// 표시·이미지 정보만 나른다.
/// </summary>
/// <param name="Name">정규 이름(예: <c>python</c>). 세션의 <c>language</c>로 되돌려 주는 값.</param>
/// <param name="DisplayName">사람에게 보여 줄 이름.</param>
/// <param name="Version">샌드박스 이미지가 제공하는 언어 버전.</param>
/// <param name="Aliases">정규 이름을 포함한, 이 언어로 해석되는 모든 이름. 대소문자 무시.</param>
/// <param name="DockerImage">Docker 런타임이 쓰는 이미지 이름(레지스트리 접두사 없음).</param>
public sealed record LanguageDescriptor(
    string Name,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Aliases,
    string DockerImage);

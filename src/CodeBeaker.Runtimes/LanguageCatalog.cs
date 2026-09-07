using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// 지원 언어의 단일 출처. 이름·별칭·표시 정보·Docker 이미지가 여기에만 있고,
/// REST/JSON-RPC 언어 조회와 Docker 런타임의 이미지 선택이 모두 이 표를 읽는다.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>
    /// 지원 언어 전체, 정규 이름 순서대로.
    /// </summary>
    public static IReadOnlyList<LanguageDescriptor> All { get; } =
    [
        new("python", "Python", "3.13", ["python", "py", "python3"], "codebeaker-python:latest"),
        new("javascript", "JavaScript (Node.js)", "24", ["javascript", "js", "node", "nodejs"], "codebeaker-nodejs:latest"),
        new("go", "Go", "1.27", ["go", "golang"], "codebeaker-golang:latest"),
        new("csharp", "C# (.NET)", "10.0", ["csharp", "cs", "dotnet"], "codebeaker-dotnet:latest"),
    ];

    private static readonly Dictionary<string, LanguageDescriptor> _byAlias = BuildIndex();

    /// <summary>
    /// 정규 이름 목록 — <c>initialize</c>가 광고하는 <c>supportedLanguages</c>.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = All.Select(l => l.Name).ToArray();

    /// <summary>
    /// 정규 이름과 별칭 전체 — 이 값 중 하나가 세션의 <c>language</c>로 들어오면 해석된다.
    /// </summary>
    public static IReadOnlyList<string> AllNames { get; } = _byAlias.Keys.ToArray();

    /// <summary>
    /// 이름 또는 별칭으로 언어를 찾는다. 대소문자를 무시하며, <see langword="null"/>·빈 문자열은 실패다.
    /// </summary>
    public static bool TryResolve(string? nameOrAlias, out LanguageDescriptor language)
    {
        if (!string.IsNullOrWhiteSpace(nameOrAlias) && _byAlias.TryGetValue(nameOrAlias, out var found))
        {
            language = found;
            return true;
        }

        language = null!;
        return false;
    }

    /// <summary>
    /// 이름 또는 별칭으로 언어를 찾는다.
    /// </summary>
    /// <exception cref="ArgumentException">이름이 비어 있음</exception>
    /// <exception cref="NotSupportedException">지원하지 않는 언어</exception>
    public static LanguageDescriptor Resolve(string nameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(nameOrAlias))
        {
            throw new ArgumentException("Language cannot be null or empty", nameof(nameOrAlias));
        }

        if (TryResolve(nameOrAlias, out var language))
        {
            return language;
        }

        throw new NotSupportedException(
            $"Language '{nameOrAlias}' is not supported. Supported languages: {string.Join(", ", Names)}");
    }

    /// <summary>
    /// 이름 또는 별칭이 지원 언어로 해석되는지 확인한다.
    /// </summary>
    public static bool IsSupported(string? nameOrAlias) => TryResolve(nameOrAlias, out _);

    private static Dictionary<string, LanguageDescriptor> BuildIndex()
    {
        var index = new Dictionary<string, LanguageDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in All)
        {
            foreach (var alias in language.Aliases)
            {
                index.Add(alias, language);
            }
        }

        return index;
    }
}

using CodeBeaker.Core.Interfaces;

namespace CodeBeaker.Runtimes;

/// <summary>
/// 런타임 팩토리 - 언어별 런타임 인스턴스 제공
/// </summary>
public static class RuntimeRegistry
{
    private static readonly Dictionary<string, Func<IRuntime>> _runtimes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "python", () => new PythonRuntime() },
        { "javascript", () => new JavaScriptRuntime() },
        { "js", () => new JavaScriptRuntime() },
        { "node", () => new JavaScriptRuntime() },
        { "go", () => new GoRuntime() },
        { "golang", () => new GoRuntime() },
        { "csharp", () => new CSharpRuntime() },
        { "cs", () => new CSharpRuntime() },
        { "dotnet", () => new CSharpRuntime() }
    };

    /// <summary>
    /// 언어별 런타임 가져오기
    /// </summary>
    /// <param name="language">언어 이름 (python, javascript, go, csharp)</param>
    /// <returns>런타임 인스턴스</returns>
    /// <exception cref="NotSupportedException">지원하지 않는 언어</exception>
    public static IRuntime Get(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be null or empty", nameof(language));
        }

        if (_runtimes.TryGetValue(language, out var factory))
        {
            return factory();
        }

        throw new NotSupportedException($"Language '{language}' is not supported. " +
                                      $"Supported languages: {string.Join(", ", GetSupportedLanguages())}");
    }

    /// <summary>
    /// 지원하는 언어 목록
    /// </summary>
    public static IReadOnlyList<string> GetSupportedLanguages()
    {
        return new[] { "python", "javascript", "go", "csharp" };
    }

    /// <summary>
    /// 언어 지원 여부 확인
    /// </summary>
    public static bool IsSupported(string language)
    {
        return !string.IsNullOrWhiteSpace(language) &&
               _runtimes.ContainsKey(language);
    }
}

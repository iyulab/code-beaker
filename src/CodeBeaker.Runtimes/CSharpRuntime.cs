namespace CodeBeaker.Runtimes;

/// <summary>
/// .NET 8 C# 런타임
/// </summary>
public sealed class CSharpRuntime : BaseRuntime
{
    public override string LanguageName => "csharp";
    public override string DockerImage => "codebeaker-dotnet:latest";
}

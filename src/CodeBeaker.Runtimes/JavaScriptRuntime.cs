namespace CodeBeaker.Runtimes;

/// <summary>
/// Node.js 20 런타임
/// </summary>
public sealed class JavaScriptRuntime : BaseRuntime
{
    public override string LanguageName => "javascript";
    public override string DockerImage => "codebeaker-nodejs:latest";
}

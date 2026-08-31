namespace CodeBeaker.Runtimes;

/// <summary>
/// Go 1.21 런타임
/// </summary>
public sealed class GoRuntime : BaseRuntime
{
    public override string LanguageName => "go";
    public override string DockerImage => "codebeaker-golang:latest";
}

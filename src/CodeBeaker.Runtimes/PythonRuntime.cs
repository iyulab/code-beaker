namespace CodeBeaker.Runtimes;

/// <summary>
/// Python 3.12 런타임
/// </summary>
public sealed class PythonRuntime : BaseRuntime
{
    public override string LanguageName => "python";
    public override string DockerImage => "codebeaker-python:latest";
}

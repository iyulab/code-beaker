namespace CodeBeaker.Runtimes;

/// <summary>
/// Python 3.12 런타임
/// </summary>
public sealed class PythonRuntime : BaseRuntime
{
    public override string LanguageName => "python";
    public override string DockerImage => "codebeaker-python:latest";
    protected override string FileExtension => ".py";

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        if (packages == null || packages.Count == 0)
        {
            return new[] { "python3", $"/workspace/{entryPoint}" };
        }

        // Install packages then run
        var packageList = string.Join(" ", packages);
        return new[]
        {
            "sh", "-c",
            $"pip install --no-cache-dir {packageList} && python3 /workspace/{entryPoint}"
        };
    }
}

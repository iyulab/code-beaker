namespace CodeBeaker.Runtimes;

/// <summary>
/// Node.js 20 런타임
/// </summary>
public sealed class JavaScriptRuntime : BaseRuntime
{
    public override string LanguageName => "javascript";
    public override string DockerImage => "codebeaker-nodejs:latest";
    protected override string FileExtension => ".js";

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        if (packages == null || packages.Count == 0)
        {
            return new[] { "node", $"/workspace/{entryPoint}" };
        }

        // Install packages then run
        var packageList = string.Join(" ", packages);
        return new[]
        {
            "sh", "-c",
            $"npm install --no-save {packageList} && node /workspace/{entryPoint}"
        };
    }
}

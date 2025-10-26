namespace CodeBeaker.Runtimes;

/// <summary>
/// Go 1.21 런타임
/// </summary>
public sealed class GoRuntime : BaseRuntime
{
    public override string LanguageName => "go";
    public override string DockerImage => "codebeaker-golang:latest";
    protected override string FileExtension => ".go";

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        // Go needs GOCACHE and GOMODCACHE in tmpfs (read-only filesystem)
        var baseCommand = "export GOCACHE=/tmp/.cache && " +
                         "export GOMODCACHE=/tmp/.modcache && " +
                         "mkdir -p /tmp/build && " +
                         $"cp /workspace/{entryPoint} /tmp/build/ && " +
                         "cd /tmp/build && ";

        if (packages != null && packages.Count > 0)
        {
            // Initialize go.mod and install packages
            baseCommand += "go mod init main && ";
            foreach (var pkg in packages)
            {
                baseCommand += $"go get {pkg} && ";
            }
        }

        baseCommand += $"go build -o /tmp/app {entryPoint} && /tmp/app";

        return new[] { "sh", "-c", baseCommand };
    }
}

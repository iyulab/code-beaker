namespace CodeBeaker.Runtimes;

/// <summary>
/// .NET 8 C# 런타임
/// </summary>
public sealed class CSharpRuntime : BaseRuntime
{
    public override string LanguageName => "csharp";
    public override string DockerImage => "codebeaker-dotnet:latest";
    protected override string FileExtension => ".cs";

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        var baseCommand = "cd /workspace && " +
                         "dotnet new console --force && " +
                         $"cp {entryPoint} Program.cs && ";

        if (packages != null && packages.Count > 0)
        {
            // Add package references
            foreach (var pkg in packages)
            {
                baseCommand += $"dotnet add package {pkg} && ";
            }
        }

        baseCommand += "dotnet run --no-restore";

        return new[] { "sh", "-c", baseCommand };
    }
}

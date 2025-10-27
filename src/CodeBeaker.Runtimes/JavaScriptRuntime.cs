using CodeBeaker.Commands.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// Node.js 20 런타임
/// </summary>
public sealed class JavaScriptRuntime : BaseRuntime
{
    public override string LanguageName => "javascript";
    public override string DockerImage => "codebeaker-nodejs:latest";
    protected override string FileExtension => ".js";

    public override List<Command> GetExecutionPlan(string code, List<string>? packages = null)
    {
        var commands = new List<Command>
        {
            // 1. Write JavaScript code to file
            new WriteFileCommand
            {
                Path = "/workspace/main.js",
                Content = code,
                Mode = FileWriteMode.Create
            }
        };

        // 2. Install npm packages if needed
        if (packages != null && packages.Count > 0)
        {
            commands.Add(new ExecuteShellCommand
            {
                CommandName = "npm",
                Args = new List<string> { "install", "--no-save" }.Concat(packages).ToList(),
                WorkingDirectory = "/workspace"
            });
        }

        // 3. Run Node.js script
        commands.Add(new ExecuteShellCommand
        {
            CommandName = "node",
            Args = new List<string> { "/workspace/main.js" },
            WorkingDirectory = "/workspace"
        });

        return commands;
    }

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

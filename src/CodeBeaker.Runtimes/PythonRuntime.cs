using CodeBeaker.Commands.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// Python 3.12 런타임
/// </summary>
public sealed class PythonRuntime : BaseRuntime
{
    public override string LanguageName => "python";
    public override string DockerImage => "codebeaker-python:latest";
    protected override string FileExtension => ".py";

    public override List<Command> GetExecutionPlan(string code, List<string>? packages = null)
    {
        var commands = new List<Command>
        {
            // 1. Write Python code to file
            new WriteFileCommand
            {
                Path = "/workspace/main.py",
                Content = code,
                Mode = FileWriteMode.Create
            }
        };

        // 2. Install packages if needed
        if (packages != null && packages.Count > 0)
        {
            commands.Add(new ExecuteShellCommand
            {
                CommandName = "pip",
                Args = new List<string> { "install", "--no-cache-dir" }.Concat(packages).ToList(),
                WorkingDirectory = "/workspace"
            });
        }

        // 3. Run Python script
        commands.Add(new ExecuteShellCommand
        {
            CommandName = "python3",
            Args = new List<string> { "/workspace/main.py" },
            WorkingDirectory = "/workspace"
        });

        return commands;
    }

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

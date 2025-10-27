using CodeBeaker.Commands.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// .NET 8 C# 런타임
/// </summary>
public sealed class CSharpRuntime : BaseRuntime
{
    public override string LanguageName => "csharp";
    public override string DockerImage => "codebeaker-dotnet:latest";
    protected override string FileExtension => ".cs";

    public override List<Command> GetExecutionPlan(string code, List<string>? packages = null)
    {
        var commands = new List<Command>
        {
            // 1. Create project directory
            new CreateDirectoryCommand
            {
                Path = "/workspace/proj",
                Recursive = true
            },

            // 2. Write source code to file
            new WriteFileCommand
            {
                Path = "/workspace/code.cs",
                Content = code,
                Mode = FileWriteMode.Create
            },

            // 3. Create new console project
            new ExecuteShellCommand
            {
                CommandName = "dotnet",
                Args = new List<string> { "new", "console", "--force" },
                WorkingDirectory = "/workspace/proj"
            },

            // 4. Copy source file to project
            new CopyFileCommand
            {
                Source = "/workspace/code.cs",
                Destination = "/workspace/proj/Program.cs",
                Overwrite = true
            }
        };

        // 5. Add package references if needed
        if (packages != null && packages.Count > 0)
        {
            foreach (var pkg in packages)
            {
                commands.Add(new ExecuteShellCommand
                {
                    CommandName = "dotnet",
                    Args = new List<string> { "add", "package", pkg },
                    WorkingDirectory = "/workspace/proj"
                });
            }
        }

        // 6. Run the code
        commands.Add(new ExecuteShellCommand
        {
            CommandName = "dotnet",
            Args = new List<string> { "run", "--no-restore" },
            WorkingDirectory = "/workspace/proj"
        });

        return commands;
    }

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        // Create project in subdirectory to avoid overwriting source file
        var baseCommand = "cd /workspace && " +
                         "mkdir -p proj && cd proj && " +
                         "dotnet new console --force && " +
                         $"cp ../{entryPoint} Program.cs && ";

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

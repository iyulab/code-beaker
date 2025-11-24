using CodeBeaker.Commands.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// Go 1.21 런타임
/// </summary>
public sealed class GoRuntime : BaseRuntime
{
    public override string LanguageName => "go";
    public override string DockerImage => "codebeaker-golang:latest";
    protected override string FileExtension => ".go";

    public override List<Command> GetExecutionPlan(string code, List<string>? packages = null)
    {
        var commands = new List<Command>
        {
            // 1. Write Go code to file
            new WriteFileCommand
            {
                Path = "/workspace/main.go",
                Content = code,
                Mode = FileWriteMode.Create
            }
        };

        // 2. Set Go cache environment variables
        var goEnv = new Dictionary<string, string>
        {
            { "GOCACHE", "/tmp/.cache" },
            { "GOMODCACHE", "/tmp/.modcache" }
        };

        // 3. Initialize go.mod if packages are needed
        if (packages != null && packages.Count > 0)
        {
            commands.Add(new ExecuteShellCommand
            {
                CommandName = "go",
                Args = new List<string> { "mod", "init", "main" },
                WorkingDirectory = "/workspace",
                Environment = goEnv
            });

            // 4. Install each package
            foreach (var pkg in packages)
            {
                commands.Add(new ExecuteShellCommand
                {
                    CommandName = "go",
                    Args = new List<string> { "get", pkg },
                    WorkingDirectory = "/workspace",
                    Environment = goEnv
                });
            }
        }

        // 5. Build the Go binary
        commands.Add(new ExecuteShellCommand
        {
            CommandName = "go",
            Args = new List<string> { "build", "-o", "/workspace/app", "main.go" },
            WorkingDirectory = "/workspace",
            Environment = goEnv
        });

        // 6. Run the binary
        commands.Add(new ExecuteShellCommand
        {
            CommandName = "/workspace/app",
            Args = new List<string>(),
            WorkingDirectory = "/workspace"
        });

        return commands;
    }

    public override string[] GetRunCommand(string entryPoint, List<string>? packages = null)
    {
        // Go needs GOCACHE and GOMODCACHE in tmpfs
        var baseCommand = "export GOCACHE=/tmp/.cache && " +
                         "export GOMODCACHE=/tmp/.modcache && " +
                         "cd /workspace && ";

        if (packages != null && packages.Count > 0)
        {
            // Initialize go.mod and install packages
            baseCommand += "go mod init main && ";
            foreach (var pkg in packages)
            {
                baseCommand += $"go get {pkg} && ";
            }
        }

        baseCommand += $"go build -o /workspace/app {entryPoint} && /workspace/app";

        return new[] { "sh", "-c", baseCommand };
    }
}

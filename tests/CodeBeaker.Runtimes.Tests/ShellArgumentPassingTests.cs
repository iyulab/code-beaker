using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Node;
using Xunit;

// Aliased: the package also exposes a legacy CodeBeaker.Runtimes.PythonRuntime (a
// language/image descriptor, unrelated to execution), and the unqualified name binds to
// that one from inside this namespace.
using PythonExecutionRuntime = CodeBeaker.Runtimes.Python.PythonRuntime;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// One invariant, checked on every runtime that spawns a process for a shell command:
/// an argument stays one argument, whatever it contains.
///
/// The runtimes used to join their argument list into a single command line, which the
/// operating system then re-parses on whitespace — so an argument holding a script or a
/// path with spaces arrived at the child as several arguments. Through a POSIX shell that
/// produced no error at all: the shell ran the fragment before the first space, wrote
/// nothing and exited successfully.
///
/// These live together rather than in a per-runtime file because the subject is the
/// invariant, not the runtime: the same defect was present in each of them, and a check
/// added to only one of them is how it came back.
///
/// The invariant has two reachable triggers, and both are here. A caller's own argument
/// containing spaces is the obvious one; the quieter one is the workspace path, which
/// runtimes interpolate into arguments they build themselves (a permission flag, the
/// script to run) — so a workspace under a directory whose name contains a space breaks
/// execution for a caller who passed nothing unusual at all.
///
/// Each case drives the runtime's own toolchain — the one that runtime already requires —
/// and observes the outcome directly rather than inferring it. A runtime whose toolchain
/// is absent is skipped.
/// </summary>
public sealed class ShellArgumentPassingTests
{
    private const string Phrase = "one two three";

    /// <summary>
    /// On Windows the interpreter is on PATH as <c>python</c>; distributions generally
    /// ship it as <c>python3</c> and may not provide the unsuffixed name at all.
    /// </summary>
    private static string PythonCommand => OperatingSystem.IsWindows() ? "python" : "python3";

    [SkippableFact]
    public async Task NodeRuntime_ShellCommand_KeepsAnArgumentContainingSpacesAsOneArgument()
    {
        var runtime = new NodeRuntime();
        Skip.IfNot(await runtime.IsAvailableAsync(), "Node.js is not installed.");

        var output = await RunAsync(
            runtime,
            new ExecuteShellCommand
            {
                CommandName = "node",
                Args = ["-e", "console.log(process.argv[1])", Phrase],
            });

        Assert.Equal(Phrase, output.Trim());
    }

    [SkippableFact]
    public async Task PythonRuntime_ShellCommand_KeepsAnArgumentContainingSpacesAsOneArgument()
    {
        var runtime = new PythonExecutionRuntime();
        Skip.IfNot(await runtime.IsAvailableAsync(), "Python is not installed.");

        var output = await RunAsync(
            runtime,
            new ExecuteShellCommand
            {
                CommandName = PythonCommand,
                Args = ["-c", "import sys; print(sys.argv[1])", Phrase],
            });

        Assert.Equal(Phrase, output.Trim());
    }

    [SkippableFact]
    public async Task DenoRuntime_CodeExecution_SurvivesAWorkspacePathContainingASpace()
    {
        var runtime = new DenoRuntime();
        Skip.IfNot(await runtime.IsAvailableAsync(), "Deno is not installed.");

        // Deno builds --allow-read=<workspace> and the script path itself, so nothing the
        // caller passes has to be unusual for the workspace path to reach the process as
        // two arguments.
        var output = await RunInSpacedWorkspaceAsync(
            runtime,
            new ExecuteCodeCommand { Language = "typescript", Code = $"console.log('{Phrase}');" });

        Assert.Contains(Phrase, output);
    }

    [SkippableFact]
    public async Task BunRuntime_CodeExecution_SurvivesAWorkspacePathContainingASpace()
    {
        var runtime = new BunRuntime();
        Skip.IfNot(await runtime.IsAvailableAsync(), "Bun is not installed.");

        var output = await RunInSpacedWorkspaceAsync(
            runtime,
            new ExecuteCodeCommand { Language = "javascript", Code = $"console.log('{Phrase}');" });

        Assert.Contains(Phrase, output);
    }

    private static Task<string> RunInSpacedWorkspaceAsync(IExecutionRuntime runtime, Command command)
        => ExecuteInAsync(runtime, Path.Combine(Path.GetTempPath(), $"shell args {Guid.NewGuid():N}"), command);

    private static Task<string> RunAsync(IExecutionRuntime runtime, ExecuteShellCommand command)
        => ExecuteInAsync(runtime, Path.Combine(Path.GetTempPath(), $"shell-args-{Guid.NewGuid():N}"), command);

    private static async Task<string> ExecuteInAsync(IExecutionRuntime runtime, string workspace, Command command)
    {
        try
        {
            var environment = await runtime.CreateEnvironmentAsync(new RuntimeConfig
            {
                Environment = runtime.Name,
                WorkspaceDirectory = workspace,
                ResourceLimits = new ResourceLimits { TimeoutSeconds = 60 },
            });

            var result = await environment.ExecuteAsync(command);
            await environment.DisposeAsync();

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Result);
            return (string)result.Result!;
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }
        }
    }
}

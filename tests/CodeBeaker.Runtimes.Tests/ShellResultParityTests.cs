using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Native;
using CodeBeaker.Runtimes.Node;
using Xunit;

// Aliased for the same reason as in ShellArgumentPassingTests: the unqualified name
// binds to an unrelated type from inside this namespace.
using PythonExecutionRuntime = CodeBeaker.Runtimes.Python.PythonRuntime;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// One invariant, checked on every runtime that runs shell commands in this process:
/// a shell command's <see cref="CommandResult"/> has the same shape whichever runtime ran it.
///
/// <see cref="CommandResult.Result"/> is a <see cref="ShellCommandOutput"/> carrying the exit
/// code and both streams; <see cref="CommandResult.Success"/> means the exit code was zero;
/// <see cref="CommandResult.Error"/> is <see langword="null"/> on success and the command's
/// standard error on failure. The runtimes used to disagree — some returned standard output
/// as a bare string and put standard error in <c>Error</c> even on success, the Docker path
/// returned an object and reported every completed command as successful — so a consumer
/// could not read a shell result without knowing which runtime produced it.
///
/// The Docker runtime is held to the same invariant against a live daemon in
/// <c>DockerShellResultLiveTests</c> (integration tests). A runtime whose toolchain is
/// absent is skipped.
///
/// Streams are compared after trimming: some runtimes collect output line by line and do
/// not keep the final newline, and line endings differ by platform. Neither is what this
/// test is about.
/// </summary>
public sealed class ShellResultParityTests
{
    public enum Scenario
    {
        /// <summary>Exit 0, standard output only.</summary>
        Succeeds,

        /// <summary>Non-zero exit with a message on standard error.</summary>
        FailsWithStderr,

        /// <summary>Exit 0 while writing to both streams, as a command printing warnings does.</summary>
        SucceedsWritingBothStreams,
    }

    private const int FailureExitCode = 3;

    public static TheoryData<string> Runtimes() => new() { "native", "node", "python", "deno", "bun" };

    /// <summary>
    /// What a result looked like, flattened so that one comparison names the scenario and
    /// every field when it fails.
    /// </summary>
    private sealed record Observed(
        Scenario Scenario, string? ResultType, int? ExitCode, bool Success, string? Stdout, string? Stderr, string? Error);

    /// <summary>
    /// One environment per runtime, reused for every scenario: creating an environment is
    /// the expensive part for some runtimes (Python builds a virtual environment), and
    /// the scenarios do not depend on each other.
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(Runtimes))]
    public async Task ShellCommand_ReturnsTheSameResultShape_OnEveryRuntime(string runtimeName)
    {
        var runtime = CreateRuntime(runtimeName);
        Skip.IfNot(await runtime.IsAvailableAsync(), $"The {runtimeName} toolchain is not installed.");

        var workspace = Path.Combine(Path.GetTempPath(), $"shell-parity-{Guid.NewGuid():N}");

        try
        {
            var environment = await runtime.CreateEnvironmentAsync(new RuntimeConfig
            {
                Environment = runtime.Name,
                WorkspaceDirectory = workspace,
                ResourceLimits = new ResourceLimits { TimeoutSeconds = 60 },
            });

            await using (environment)
            {
                foreach (var scenario in Enum.GetValues<Scenario>())
                {
                    var (exitCode, writesStdout, writesStderr) = Describe(scenario);
                    var commands = BuildCommands(runtimeName, writesStdout, writesStderr, exitCode);
                    foreach (var setup in commands.Setup)
                    {
                        var setupResult = await environment.ExecuteAsync(setup);
                        Assert.True(setupResult.Success, setupResult.Error);
                    }

                    var result = await environment.ExecuteAsync(commands.Shell);
                    var output = result.Result as ShellCommandOutput;

                    var expected = new Observed(
                        scenario,
                        nameof(ShellCommandOutput),
                        exitCode,
                        exitCode == 0,
                        writesStdout ? "out" : string.Empty,
                        writesStderr ? "err" : string.Empty,
                        exitCode == 0 ? null : "err");
                    var actual = new Observed(
                        scenario,
                        result.Result?.GetType().Name,
                        output?.ExitCode,
                        result.Success,
                        output?.Stdout.Trim(),
                        output?.Stderr.Trim(),
                        result.Error?.Trim());

                    Assert.Equal(expected, actual);
                }
            }
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }
        }
    }

    private static (int ExitCode, bool WritesStdout, bool WritesStderr) Describe(Scenario scenario) => scenario switch
    {
        Scenario.Succeeds => (0, true, false),
        Scenario.FailsWithStderr => (FailureExitCode, false, true),
        Scenario.SucceedsWritingBothStreams => (0, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
    };

    private static IExecutionRuntime CreateRuntime(string name) => name switch
    {
        "native" => new NativeProcessRuntime(),
        "node" => new NodeRuntime(),
        "python" => new PythonExecutionRuntime(),
        "deno" => new DenoRuntime(),
        "bun" => new BunRuntime(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static (IReadOnlyList<Command> Setup, ExecuteShellCommand Shell) BuildCommands(
        string runtimeName, bool stdout, bool stderr, int exitCode)
    {
        switch (runtimeName)
        {
            case "native":
                return ([], NativeShell(stdout, stderr, exitCode));

            case "node":
                return ([], new ExecuteShellCommand { CommandName = "node", Args = ["-e", JavaScript(stdout, stderr, exitCode)] });

            case "python":
                var python = new List<string> { "import sys" };
                if (stdout) python.Add("sys.stdout.write('out\\n')");
                if (stderr) python.Add("sys.stderr.write('err\\n')");
                python.Add($"sys.exit({exitCode})");
                return ([], new ExecuteShellCommand
                {
                    CommandName = OperatingSystem.IsWindows() ? "python" : "python3",
                    Args = ["-c", string.Join("; ", python)],
                });

            case "deno":
                // The Deno runtime turns a `deno` shell command into `deno run <permissions>`,
                // so the script goes in a workspace file rather than through `deno eval`.
                var deno = new List<string>();
                if (stdout) deno.Add("console.log('out')");
                if (stderr) deno.Add("console.error('err')");
                deno.Add($"Deno.exit({exitCode})");
                return (
                    [new WriteFileCommand { Path = "parity.js", Content = string.Join("; ", deno) }],
                    new ExecuteShellCommand { CommandName = "deno", Args = ["parity.js"] });

            case "bun":
                // Bun's shell command runs a script from the workspace (`bun run <file>`)
                // rather than an arbitrary executable, so the script is written first.
                return (
                    [new WriteFileCommand { Path = "parity.js", Content = JavaScript(stdout, stderr, exitCode) }],
                    new ExecuteShellCommand { CommandName = "parity.js" });

            default:
                throw new ArgumentOutOfRangeException(nameof(runtimeName), runtimeName, null);
        }
    }

    private static string JavaScript(bool stdout, bool stderr, int exitCode)
    {
        var script = new List<string>();
        if (stdout) script.Add("process.stdout.write('out\\n')");
        if (stderr) script.Add("process.stderr.write('err\\n')");
        script.Add($"process.exitCode = {exitCode}");
        return string.Join("; ", script);
    }

    /// <summary>
    /// The native runtime spawns whatever it is given, so the platform's shell does the
    /// writing. <c>(&gt;&amp;2 echo err)</c> rather than <c>echo err 1&gt;&amp;2</c>: cmd keeps
    /// the space before a trailing redirection as part of the echoed text.
    /// </summary>
    private static ExecuteShellCommand NativeShell(bool stdout, bool stderr, int exitCode)
    {
        if (OperatingSystem.IsWindows())
        {
            var parts = new List<string>();
            if (stdout) parts.Add("echo out");
            if (stderr) parts.Add("(>&2 echo err)");
            parts.Add($"exit /b {exitCode}");
            return new ExecuteShellCommand { CommandName = "cmd", Args = ["/c", string.Join("&", parts)] };
        }

        var posix = new List<string>();
        if (stdout) posix.Add("echo out");
        if (stderr) posix.Add("echo err >&2");
        posix.Add($"exit {exitCode}");
        return new ExecuteShellCommand { CommandName = "sh", Args = ["-c", string.Join("; ", posix)] };
    }
}

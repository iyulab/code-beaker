using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Native;
using CodeBeaker.Runtimes.Node;
using Xunit;

// Aliased for the same reason as in ShellArgumentPassingTests: the unqualified name
// binds to an unrelated type from inside this namespace.
using PythonExecutionRuntime = CodeBeaker.Runtimes.Python.PythonRuntime;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// How a shell command runs, checked on every runtime that runs it as a local process: both
/// streams are drained together, the environment's timeout bounds it, and the command's own
/// working directory and environment variables are applied. The Node and Python runtimes used
/// to read standard output to its end before standard error (a child filling the stderr pipe
/// then blocked forever), had no timeout, and ignored the command's working directory and
/// environment. A runtime whose toolchain is absent is skipped.
/// </summary>
public sealed class ShellProcessBehaviourTests
{
    /// <summary>Guards each test against the hang it is about, so a regression fails instead
    /// of stalling the suite.</summary>
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(150);

    public static TheoryData<string> Runtimes() => new() { "native", "node", "python" };

    [SkippableTheory]
    [MemberData(nameof(Runtimes))]
    public async Task ShellCommand_WritingMoreStderrThanThePipeBuffer_DoesNotDeadlock(string runtimeName)
    {
        // ~1MB on standard error with some standard output, well past any OS pipe buffer.
        var line = new string('x', 500);
        // The limit is generous for the reason given on the native runtime's large-output test.
        var result = await RunAsync(runtimeName, timeoutSeconds: 90, Shell(
            $"echo start & for /L %i in (1,1,2000) do @echo {line} 1>&2",
            $"echo start; i=0; while [ $i -lt 2000 ]; do echo {line} 1>&2; i=$((i+1)); done"));

        Assert.True(result.Success, result.Error);
        var output = Assert.IsType<ShellCommandOutput>(result.Result);
        Assert.Equal("start", output.Stdout.Trim());
        Assert.True(output.Stderr.Length > 800_000);
    }

    [SkippableTheory]
    [MemberData(nameof(Runtimes))]
    public async Task ShellCommand_RunningPastTheTimeout_IsKilledAndReportedAsATimeout(string runtimeName)
    {
        var result = await RunAsync(runtimeName, timeoutSeconds: 2, Shell(
            "ping -n 30 127.0.0.1 >nul",
            "sleep 30"));

        // The message carries the whole result: this has failed intermittently under load
        // without reproducing on re-run, and a bare boolean failure left nothing to go on.
        Assert.False(result.Success, $"Expected a timeout, got a successful result: {result.Result}");
        Assert.Equal("Execution timeout (2s)", result.Error);
        Assert.InRange(result.DurationMs, 1_000, 20_000);
    }

    [SkippableTheory]
    [MemberData(nameof(Runtimes))]
    public async Task ShellCommand_RunsInItsOwnWorkingDirectory_WithItsOwnEnvironment(string runtimeName)
    {
        var directory = Directory.CreateTempSubdirectory("shell-cwd-").FullName;
        try
        {
            var command = Shell("cd & echo %CODEBEAKER_PROBE%", "pwd; echo $CODEBEAKER_PROBE");
            command.WorkingDirectory = directory;
            command.Environment = new Dictionary<string, string> { ["CODEBEAKER_PROBE"] = "probe-value" };

            var result = await RunAsync(runtimeName, timeoutSeconds: 30, command);

            Assert.True(result.Success, result.Error);
            var lines = Assert.IsType<ShellCommandOutput>(result.Result).Stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(
                [Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), "probe-value"],
                [Path.GetFullPath(lines[0]).TrimEnd(Path.DirectorySeparatorChar), lines[1]]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task<CommandResult> RunAsync(string runtimeName, int timeoutSeconds, ExecuteShellCommand command)
    {
        IExecutionRuntime runtime = runtimeName switch
        {
            "native" => new NativeProcessRuntime(),
            "node" => new NodeRuntime(),
            "python" => new PythonExecutionRuntime(),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimeName), runtimeName, null),
        };
        Skip.IfNot(await runtime.IsAvailableAsync(), $"The {runtimeName} toolchain is not installed.");

        var workspace = Path.Combine(Path.GetTempPath(), $"shell-behaviour-{Guid.NewGuid():N}");
        try
        {
            var environment = await runtime.CreateEnvironmentAsync(new RuntimeConfig
            {
                Environment = runtime.Name,
                WorkspaceDirectory = workspace,
                ResourceLimits = new ResourceLimits { TimeoutSeconds = timeoutSeconds },
            });

            await using (environment)
            {
                return await environment.ExecuteAsync(command).WaitAsync(HangGuard);
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

    private static ExecuteShellCommand Shell(string windowsScript, string posixScript)
        => OperatingSystem.IsWindows()
            ? new ExecuteShellCommand { CommandName = "cmd", Args = ["/c", windowsScript] }
            : new ExecuteShellCommand { CommandName = "sh", Args = ["-c", posixScript] };
}

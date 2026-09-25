using System.Diagnostics;
using CodeBeaker.Commands.Models;

namespace CodeBeaker.Runtimes;

/// <summary>
/// Runs one <see cref="ExecuteShellCommand"/> as a local OS process — the part every in-process
/// runtime shares, so each one drains, bounds and reports a shell command the same way.
/// </summary>
internal static class ShellProcess
{
    /// <summary>The timeout applied when the environment's resource limits set none.</summary>
    public const int DefaultTimeoutSeconds = 300;

    /// <param name="command">The command; its own working directory and environment win.</param>
    /// <param name="defaultWorkingDirectory">Used when the command names no working directory.</param>
    /// <param name="timeoutSeconds">Bounds exit and both stream drains together.</param>
    /// <param name="track">Receives the process once started and <see langword="null"/> once
    /// finished, so the owning runtime can kill it from cleanup.</param>
    public static async Task<CommandResult> RunAsync(
        ExecuteShellCommand command,
        string defaultWorkingDirectory,
        int timeoutSeconds,
        Action<Process?> track,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // ArgumentList, not Arguments: a joined string is re-parsed on whitespace, so any
        // single argument that contains a space (a shell script, a path with spaces) silently
        // reaches the child as several arguments. ArgumentList escapes each element for the
        // platform and passes them through intact.
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(command.CommandName, command.Args)
            {
                WorkingDirectory = command.WorkingDirectory ?? defaultWorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (command.Environment is not null)
        {
            foreach (var (key, value) in command.Environment)
            {
                process.StartInfo.EnvironmentVariables[key] = value;
            }
        }

        try
        {
            process.Start();
            track(process);

            // Started immediately, not one after the other or after the exit wait: a child that
            // writes enough to fill an OS pipe buffer (~4KB on Windows) blocks on that write
            // until someone drains the pipe. Reading one stream to its end before the other, or
            // waiting for exit before reading, leaves the child blocked on the undrained pipe
            // while the parent waits for it — the classic redirected-process deadlock.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var exitTask = process.WaitForExitAsync(cancellationToken);

            // The timeout covers exit AND both drains as one bounded operation, not just the
            // exit wait: a command can launch a detached descendant that outlives it (e.g.
            // Windows "start /b" starting a dev server) and inherits the redirected pipe
            // handles. The immediate process then exits in time, but the pipe's write end stays
            // open while that descendant runs — so the reads never see EOF.
            var allDone = Task.WhenAll(exitTask, stdoutTask, stderrTask);
            var winner = await Task.WhenAny(allDone, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken));

            if (winner != allDone)
            {
                // Kill(entireProcessTree: true) resolves both hang shapes: it kills the process
                // if it is still running, and also finds and kills a lingering descendant that
                // holds the pipe open, even when the immediate process has already exited.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Already exited — Kill(true) still walked and killed any descendants.
                }

                stopwatch.Stop();
                return new CommandResult
                {
                    Success = false,
                    Error = $"Execution timeout ({timeoutSeconds}s)",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }

            var output = await stdoutTask;
            var error = await stderrTask;
            stopwatch.Stop();

            return CommandResult.FromShellOutput(
                new ShellCommandOutput(process.ExitCode, output, error),
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CommandResult { Success = false, Error = ex.Message, DurationMs = (int)stopwatch.ElapsedMilliseconds };
        }
        finally
        {
            track(null);
        }
    }
}

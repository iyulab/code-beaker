using System.Text.Json.Serialization;

namespace CodeBeaker.Commands.Models;

/// <summary>
/// What a finished <see cref="ExecuteShellCommand"/> produced, and the value of
/// <see cref="CommandResult.Result"/> for one — on every runtime.
/// </summary>
/// <remarks>
/// A command that ran to completion always carries this, whatever its exit code: a
/// non-zero exit is a failed command, not a missing result, and its output is usually
/// what explains the failure. Only a command that did not finish (a timeout, a process
/// that could not be started) has no output, and then <see cref="CommandResult.Result"/>
/// is <see langword="null"/>. Use <see cref="CommandResult.FromShellOutput"/> to build the
/// result, so that <see cref="CommandResult.Success"/> and <see cref="CommandResult.Error"/>
/// are derived from the exit code the same way everywhere.
/// </remarks>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Stdout">Everything the command wrote to standard output.</param>
/// <param name="Stderr">Everything the command wrote to standard error.</param>
public sealed record ShellCommandOutput(
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("stdout")] string Stdout,
    [property: JsonPropertyName("stderr")] string Stderr);

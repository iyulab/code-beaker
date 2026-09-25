using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Integration.Tests.TestHelpers;
using CodeBeaker.Runtimes.Docker;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// The Docker runtime's half of the shell-result invariant that <c>ShellResultParityTests</c>
/// checks for the in-process runtimes: <see cref="CommandResult.Result"/> is a
/// <see cref="ShellCommandOutput"/>, <see cref="CommandResult.Success"/> means exit code
/// zero, and <see cref="CommandResult.Error"/> is the standard error of a failed command.
///
/// This path used to report every command that ran to completion as successful, whatever
/// its exit code, and returned an object where the other runtimes returned a string. The
/// exit code here is read back from the daemon after the exec finishes, so a mock of the
/// Docker client proves the mapping but not that the daemon reports what the mapping
/// expects — that is what this checks.
/// </summary>
public sealed class DockerShellResultLiveTests
{
    public static TheoryData<string, int, string, string> Cases() => new()
    {
        { "echo out", 0, "out", "" },
        { "echo err >&2; exit 3", 3, "", "err" },
        { "echo out; echo err >&2", 0, "out", "err" },
    };

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public async Task ShellCommand_ReturnsTheSameResultShape_AsTheOtherRuntimes(
        string script, int exitCode, string stdout, string stderr)
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);

        var environment = await new DockerRuntime().CreateEnvironmentAsync(
            new RuntimeConfig { Environment = "python" });

        await using (environment)
        {
            var result = await environment.ExecuteAsync(
                new ExecuteShellCommand { CommandName = "sh", Args = ["-c", script] });

            var output = Assert.IsType<ShellCommandOutput>(result.Result);
            Assert.Equal(exitCode, output.ExitCode);
            Assert.Equal(exitCode == 0, result.Success);
            Assert.Equal(stdout, output.Stdout.Trim());
            Assert.Equal(stderr, output.Stderr.Trim());

            if (exitCode == 0)
            {
                Assert.Null(result.Error);
            }
            else
            {
                Assert.Equal(stderr, result.Error);
            }
        }
    }
}

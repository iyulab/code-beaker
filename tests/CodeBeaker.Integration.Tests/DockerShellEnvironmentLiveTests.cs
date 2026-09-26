using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using CodeBeaker.Integration.Tests.TestHelpers;
using CodeBeaker.Runtimes.Docker;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// A shell command's <see cref="ExecuteShellCommand.Environment"/> reaches that process in the
/// container, through the session path with sandboxing on — and only that process. A caller
/// can therefore hand one command a secret (a clone's credential, say) without it being
/// visible to what runs after it in the same session.
/// </summary>
public sealed class DockerShellEnvironmentLiveTests
{
    [SkippableFact]
    public async Task ShellEnvironment_ReachesThatCommand_AndNotTheNextOne()
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);

        var sessions = new SessionManager(new InMemorySessionStore(), new List<IExecutionRuntime> { new DockerRuntime() });
        var session = await sessions.CreateSessionAsync(new SessionConfig
        {
            Language = "python",
            Security = new SecurityConfig { EnableSandbox = true, SandboxRestrictFilesystem = true },
        });

        try
        {
            var withSecret = await sessions.ExecuteInSessionAsync(session.SessionId, new ExecuteShellCommand
            {
                CommandName = "sh",
                Args = ["-c", "printf %s \"$ONE_COMMAND_SECRET\""],
                Environment = new Dictionary<string, string> { ["ONE_COMMAND_SECRET"] = "only-here" },
            });
            var after = await sessions.ExecuteInSessionAsync(session.SessionId, new ExecuteShellCommand
            {
                CommandName = "sh",
                Args = ["-c", "printf %s \"${ONE_COMMAND_SECRET:-absent}\""],
            });

            Assert.Equal("only-here", Assert.IsType<ShellCommandOutput>(withSecret.Result).Stdout);
            Assert.Equal("absent", Assert.IsType<ShellCommandOutput>(after.Result).Stdout);
        }
        finally
        {
            await sessions.CloseSessionAsync(session.SessionId);
        }
    }
}

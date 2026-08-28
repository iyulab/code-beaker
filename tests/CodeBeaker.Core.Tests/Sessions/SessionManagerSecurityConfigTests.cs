using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Core.Storage;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Sessions;

/// <summary>
/// Covers <see cref="SessionManager.CreateSessionAsync"/> actually threading
/// <see cref="SecurityConfig"/> into the <see cref="RuntimeConfig.Permissions"/> it
/// hands the selected runtime, instead of the previously hardcoded values.
/// </summary>
public sealed class SessionManagerSecurityConfigTests
{
    private static (SessionManager Manager, Mock<IExecutionRuntime> Runtime) BuildManagerWithMockRuntime()
    {
        var runtime = new Mock<IExecutionRuntime>();
        runtime.Setup(r => r.Type).Returns(RuntimeType.Deno);
        runtime.Setup(r => r.SupportedEnvironments).Returns(["javascript"]);
        runtime.Setup(r => r.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runtime
            .Setup(r => r.CreateEnvironmentAsync(It.IsAny<RuntimeConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RuntimeConfig _, CancellationToken _) =>
            {
                var env = new Mock<IExecutionEnvironment>();
                env.Setup(e => e.EnvironmentId).Returns(Guid.NewGuid().ToString("N"));
                env.Setup(e => e.RuntimeType).Returns(RuntimeType.Deno);
                env.Setup(e => e.State).Returns(EnvironmentState.Ready);
                return env.Object;
            });

        var manager = new SessionManager(new InMemorySessionStore(), [runtime.Object]);
        return (manager, runtime);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, false, false)]
    public async Task CreateSessionAsync_PassesSecurityConfigThroughToRuntimePermissions(
        bool sandboxDisableNetwork, bool sandboxDisableShellCommands, bool expectedAllowNet, bool expectedAllowRun)
    {
        var (manager, runtime) = BuildManagerWithMockRuntime();
        var config = new SessionConfig
        {
            Language = "javascript",
            RuntimeType = RuntimeType.Deno,
            Security = new SecurityConfig
            {
                SandboxDisableNetwork = sandboxDisableNetwork,
                SandboxDisableShellCommands = sandboxDisableShellCommands,
            },
        };

        await manager.CreateSessionAsync(config);

        runtime.Verify(r => r.CreateEnvironmentAsync(
            It.Is<RuntimeConfig>(rc =>
                rc.Permissions!.AllowNet == expectedAllowNet &&
                rc.Permissions.AllowRun == expectedAllowRun),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task CreateSessionAsync_AlwaysScopesFilesystemAccessToTheWorkspace()
    {
        // SandboxRestrictFilesystem=false is not yet honored — DenoRuntime's flag
        // builder only knows how to emit `--allow-read=<path>` per path, not a
        // bare `--allow-read` for unrestricted access. This documents that gap
        // as a passing test rather than a silent one.
        var (manager, runtime) = BuildManagerWithMockRuntime();
        var config = new SessionConfig
        {
            Language = "javascript",
            RuntimeType = RuntimeType.Deno,
            Security = new SecurityConfig { SandboxRestrictFilesystem = false },
        };

        await manager.CreateSessionAsync(config);

        runtime.Verify(r => r.CreateEnvironmentAsync(
            It.Is<RuntimeConfig>(rc =>
                rc.Permissions!.AllowRead.SequenceEqual(new[] { "/workspace", "/tmp" }) &&
                rc.Permissions.AllowWrite.SequenceEqual(new[] { "/workspace", "/tmp" })),
            It.IsAny<CancellationToken>()));
    }
}

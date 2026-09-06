using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Sessions;

/// <summary>
/// 다른 API 인스턴스/재시작 이후의 세션 재연결 테스트.
/// 이전에는 SessionManager가 재연결 능력을 가진 런타임을 두고도
/// 무조건 null을 반환해, 살아 있는 컨테이너를 가진 세션이 조용히 무력화됐다.
/// </summary>
public sealed class SessionReconnectTests
{
    private const string Language = "python";

    /// <summary>재연결을 지원하는 런타임(컨테이너처럼 호스트보다 오래 사는 환경).</summary>
    private sealed class ReconnectableFakeRuntime : IExecutionRuntime, IReconnectableRuntime
    {
        private readonly IExecutionEnvironment? _reconnected;
        private readonly Exception? _throws;

        public ReconnectableFakeRuntime(IExecutionEnvironment? reconnected, Exception? throws = null)
        {
            _reconnected = reconnected;
            _throws = throws;
        }

        public string? LastEnvironmentId { get; private set; }
        public RuntimeConfig? LastConfig { get; private set; }

        public string Name => "fake-docker";
        public RuntimeType Type => RuntimeType.Docker;
        public string[] SupportedEnvironments => new[] { Language };
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IExecutionEnvironment> CreateEnvironmentAsync(RuntimeConfig config, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public RuntimeCapabilities GetCapabilities() => new();

        public Task<IExecutionEnvironment?> ReconnectEnvironmentAsync(
            string environmentId,
            RuntimeConfig config,
            CancellationToken cancellationToken = default)
        {
            LastEnvironmentId = environmentId;
            LastConfig = config;

            if (_throws != null)
            {
                throw _throws;
            }

            return Task.FromResult(_reconnected);
        }
    }

    /// <summary>프로세스 기반 런타임 — API 프로세스와 수명을 같이 해 재연결이 불가능하다.</summary>
    private sealed class ProcessFakeRuntime : IExecutionRuntime
    {
        public string Name => "fake-process";
        public RuntimeType Type => RuntimeType.Docker;
        public string[] SupportedEnvironments => new[] { Language };
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IExecutionEnvironment> CreateEnvironmentAsync(RuntimeConfig config, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public RuntimeCapabilities GetCapabilities() => new();
    }

    private static Session SessionWith(string containerId, bool disableNetwork = false)
    {
        return new Session
        {
            SessionId = "session-1",
            ContainerId = containerId,
            Language = Language,
            RuntimeType = RuntimeType.Docker,
            State = SessionState.Active,
            Config = new SessionConfig
            {
                Language = Language,
                MemoryLimitMB = 256,
                Security = new SecurityConfig { SandboxDisableNetwork = disableNetwork }
            }
        };
    }

    private static SessionManager ManagerWith(IExecutionRuntime runtime)
    {
        return new SessionManager(Mock.Of<ISessionStore>(), new[] { runtime });
    }

    [Fact]
    public async Task ShouldReconnect_WhenTheRuntimeSupportsIt()
    {
        var environment = Mock.Of<IExecutionEnvironment>();
        var runtime = new ReconnectableFakeRuntime(environment);
        using var manager = ManagerWith(runtime);

        var result = await manager.ReconstructEnvironmentAsync(SessionWith("container-abc"), CancellationToken.None);

        result.Should().BeSameAs(environment);
        runtime.LastEnvironmentId.Should().Be("container-abc");
    }

    [Fact]
    public async Task ShouldReconnectWithTheSessionsOwnPermissions()
    {
        // 재연결된 세션이 원래보다 느슨한 권한으로 살아나면 안 된다.
        var runtime = new ReconnectableFakeRuntime(Mock.Of<IExecutionEnvironment>());
        using var manager = ManagerWith(runtime);

        await manager.ReconstructEnvironmentAsync(
            SessionWith("container-abc", disableNetwork: true),
            CancellationToken.None);

        runtime.LastConfig.Should().NotBeNull();
        runtime.LastConfig!.Permissions!.AllowNet.Should().BeFalse();
        runtime.LastConfig.ResourceLimits!.MemoryLimitMB.Should().Be(256);
        runtime.LastConfig.Environment.Should().Be(Language);
    }

    [Fact]
    public async Task ShouldNotReconnect_WhenTheRuntimeCannot()
    {
        using var manager = ManagerWith(new ProcessFakeRuntime());

        var result = await manager.ReconstructEnvironmentAsync(SessionWith("container-abc"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNotReconnect_WhenTheSessionHasNoEnvironmentId()
    {
        var runtime = new ReconnectableFakeRuntime(Mock.Of<IExecutionEnvironment>());
        using var manager = ManagerWith(runtime);

        var result = await manager.ReconstructEnvironmentAsync(SessionWith(string.Empty), CancellationToken.None);

        result.Should().BeNull();
        runtime.LastEnvironmentId.Should().BeNull();
    }

    [Fact]
    public async Task ShouldReturnNull_WhenReconnectingThrows()
    {
        var runtime = new ReconnectableFakeRuntime(null, new InvalidOperationException("daemon unreachable"));
        using var manager = ManagerWith(runtime);

        var result = await manager.ReconstructEnvironmentAsync(SessionWith("container-abc"), CancellationToken.None);

        result.Should().BeNull();
    }
}

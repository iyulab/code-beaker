using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Sessions;
using CodeBeaker.Integration.Tests.TestHelpers;
using Docker.DotNet;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// 세션 설정이 실제 컨테이너의 HostConfig 로 반영되는지 살아있는 데몬에 대고 확인한다.
///
/// 단위 테스트(<c>DockerRuntimeHostConfigTests</c>)는 설정 → HostConfig 매핑까지만 본다.
/// 그 HostConfig 가 데몬에 도달해 컨테이너에 실제로 적용됐는지는 별개의 사실이며,
/// 격리 수준(네트워크)과 자원 상한(메모리)은 둘 다 소비자가 의존하는 계약이라
/// 매핑이 맞다는 것만으로 충족됐다고 볼 수 없다.
/// </summary>
public sealed class DockerHostConfigLiveTests : IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly DockerClient _docker = new DockerClientBuilder().Build();

    public DockerHostConfigLiveTests()
    {
        var runtimes = new List<IExecutionRuntime>
        {
            new CodeBeaker.Runtimes.Docker.DockerRuntime()
        };
        _sessionManager = new SessionManager(
            new CodeBeaker.Core.Storage.InMemorySessionStore(), runtimes);
    }

    private static async Task SkipIfDockerUnavailableAsync()
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);
    }

    private async Task<T> WithSessionAsync<T>(
        SessionConfig config, Func<string, Task<T>> inspect)
    {
        var session = await _sessionManager.CreateSessionAsync(config);
        try
        {
            Assert.NotEmpty(session.ContainerId);
            return await inspect(session.ContainerId);
        }
        finally
        {
            await _sessionManager.CloseSessionAsync(session.SessionId);
        }
    }

    [SkippableFact]
    public async Task DefaultSecurity_ContainerActuallyJoinsBridgeNetwork()
    {
        await SkipIfDockerUnavailableAsync();

        var networkMode = await WithSessionAsync(
            new SessionConfig { Language = "python", RuntimeType = RuntimeType.Docker },
            async containerId =>
            {
                var inspect = await _docker.Containers.InspectContainerAsync(containerId);
                Assert.NotNull(inspect.HostConfig);
                return inspect.HostConfig.NetworkMode;
            });

        Assert.Equal("bridge", networkMode);
    }

    [SkippableFact]
    public async Task NetworkDisabled_ContainerActuallyHasNoNetwork()
    {
        await SkipIfDockerUnavailableAsync();

        var config = new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker,
            Security = new SecurityConfig { SandboxDisableNetwork = true }
        };

        var networkMode = await WithSessionAsync(config, async containerId =>
        {
            var inspect = await _docker.Containers.InspectContainerAsync(containerId);
            Assert.NotNull(inspect.HostConfig);
            return inspect.HostConfig.NetworkMode;
        });

        Assert.Equal("none", networkMode);
    }

    [SkippableFact]
    public async Task MemoryLimit_ReachesTheContainerAsBytes()
    {
        await SkipIfDockerUnavailableAsync();

        const long limitMb = 256;
        var config = new SessionConfig
        {
            Language = "python",
            RuntimeType = RuntimeType.Docker,
            MemoryLimitMB = limitMb
        };

        var memory = await WithSessionAsync(config, async containerId =>
        {
            var inspect = await _docker.Containers.InspectContainerAsync(containerId);
            Assert.NotNull(inspect.HostConfig);
            return inspect.HostConfig.Memory;
        });

        Assert.Equal(limitMb * 1024 * 1024, memory);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
        _docker.Dispose();
    }
}

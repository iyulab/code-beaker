using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Docker;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// DockerEnvironment 가 컨테이너에 적용하는 HostConfig 단위 테스트.
/// 네트워크 개방 여부가 PermissionSettings.AllowNet 을 실제로 따르는지 고정한다
/// (이전에는 광고된 SupportsNetworkAccess 와 무관하게 항상 차단돼 있었다).
/// </summary>
public sealed class DockerRuntimeHostConfigTests
{
    [Fact]
    public void BuildHostConfig_ShouldEnableNetwork_WhenAllowNetIsTrue()
    {
        var permissions = new PermissionSettings { AllowNet = true };

        var hostConfig = DockerEnvironment.BuildHostConfig(null, permissions);

        Assert.Equal("bridge", hostConfig.NetworkMode);
    }

    [Fact]
    public void BuildHostConfig_ShouldDisableNetwork_WhenAllowNetIsFalse()
    {
        var permissions = new PermissionSettings { AllowNet = false };

        var hostConfig = DockerEnvironment.BuildHostConfig(null, permissions);

        Assert.Equal("none", hostConfig.NetworkMode);
    }

    [Fact]
    public void BuildHostConfig_ShouldDisableNetwork_WhenPermissionsAreNotSupplied()
    {
        // 권한을 표명하지 않은 호출자에게는 격리를 유지한다(fail-safe).
        var hostConfig = DockerEnvironment.BuildHostConfig(null, null);

        Assert.Equal("none", hostConfig.NetworkMode);
    }

    [Fact]
    public void BuildHostConfig_ShouldApplyDefaultResourceLimits_WhenLimitsAreNotSupplied()
    {
        var hostConfig = DockerEnvironment.BuildHostConfig(null, null);

        Assert.Equal(512L * 1024 * 1024, hostConfig.Memory);
        Assert.Equal(1024L, hostConfig.CPUShares);
        Assert.False(hostConfig.AutoRemove);
    }

    [Fact]
    public void BuildHostConfig_ShouldMapExplicitResourceLimits()
    {
        var limits = new ResourceLimits
        {
            MemoryLimitBytes = 256L * 1024 * 1024,
            CpuShares = 512,
            MaxProcesses = 64
        };

        var hostConfig = DockerEnvironment.BuildHostConfig(limits, new PermissionSettings { AllowNet = true });

        Assert.Equal(256L * 1024 * 1024, hostConfig.Memory);
        Assert.Equal(512L, hostConfig.CPUShares);
        Assert.Equal(64L, hostConfig.PidsLimit);
        Assert.Equal("bridge", hostConfig.NetworkMode);
    }
}

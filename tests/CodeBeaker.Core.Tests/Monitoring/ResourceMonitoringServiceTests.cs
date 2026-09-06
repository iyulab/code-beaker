using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Monitoring;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Monitoring;

/// <summary>
/// 리소스 제한이 실제로 강제되는지에 대한 테스트.
/// 이전에는 위반을 감지해도 경고 로그만 남기고 세션이 계속 실행됐다
/// (자동 종료 코드가 주석 처리된 채 남아 있었음).
/// </summary>
public sealed class ResourceMonitoringServiceTests
{
    private readonly Mock<ISessionManager> _sessionManager = new();

    private static Mock<IExecutionEnvironment> Environment(
        long memoryBytes,
        EnvironmentState state = EnvironmentState.Running)
    {
        var env = new Mock<IExecutionEnvironment>();
        env.Setup(e => e.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        env.Setup(e => e.GetResourceUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceUsage
            {
                MemoryUsageBytes = memoryBytes,
                CpuUsagePercent = 1.0,
                ProcessCount = 1
            });
        return env;
    }

    private static Session SessionWith(IExecutionEnvironment environment, long? memoryLimitMB)
    {
        return new Session
        {
            SessionId = "session-1",
            State = SessionState.Active,
            Config = new SessionConfig { MemoryLimitMB = memoryLimitMB },
            Environment = environment
        };
    }

    private ResourceMonitoringService CreateService(bool enableAutoTermination = true)
    {
        return new ResourceMonitoringService(
            _sessionManager.Object,
            NullLogger<ResourceMonitoringService>.Instance,
            TimeSpan.FromSeconds(5),
            enableAutoTermination);
    }

    [Fact]
    public async Task ShouldTerminateSession_WhenHardMemoryLimitIsExceeded()
    {
        var env = Environment(memoryBytes: 128L * 1024 * 1024);
        var session = SessionWith(env.Object, memoryLimitMB: 64);

        await CreateService().CheckSessionResourcesAsync(session, CancellationToken.None);

        _sessionManager.Verify(
            m => m.CloseSessionAsync("session-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldNotTerminateSession_WhenAutoTerminationIsDisabled()
    {
        var env = Environment(memoryBytes: 128L * 1024 * 1024);
        var session = SessionWith(env.Object, memoryLimitMB: 64);

        await CreateService(enableAutoTermination: false)
            .CheckSessionResourcesAsync(session, CancellationToken.None);

        _sessionManager.Verify(
            m => m.CloseSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShouldNotTerminateSession_WhenUsageIsWithinTheLimit()
    {
        var env = Environment(memoryBytes: 32L * 1024 * 1024);
        var session = SessionWith(env.Object, memoryLimitMB: 64);

        await CreateService().CheckSessionResourcesAsync(session, CancellationToken.None);

        _sessionManager.Verify(
            m => m.CloseSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShouldNotTerminateSession_WhenNoLimitIsConfigured()
    {
        var env = Environment(memoryBytes: 8L * 1024 * 1024 * 1024);
        var session = SessionWith(env.Object, memoryLimitMB: null);

        await CreateService().CheckSessionResourcesAsync(session, CancellationToken.None);

        _sessionManager.Verify(
            m => m.CloseSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShouldSkipSession_WhenEnvironmentIsAlreadyStopped()
    {
        var env = Environment(memoryBytes: 128L * 1024 * 1024, state: EnvironmentState.Stopped);
        var session = SessionWith(env.Object, memoryLimitMB: 64);

        await CreateService().CheckSessionResourcesAsync(session, CancellationToken.None);

        _sessionManager.Verify(
            m => m.CloseSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        env.Verify(e => e.GetResourceUsageAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldAccumulateUsageHistory_AcrossChecksOfTheSameSession()
    {
        var env = Environment(memoryBytes: 32L * 1024 * 1024);
        var session = SessionWith(env.Object, memoryLimitMB: 64);
        var service = CreateService();

        await service.CheckSessionResourcesAsync(session, CancellationToken.None);
        await service.CheckSessionResourcesAsync(session, CancellationToken.None);

        // 같은 세션에는 같은 모니터가 재사용되므로 폴링마다 사용량이 조회된다.
        env.Verify(
            e => e.GetResourceUsageAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }
}

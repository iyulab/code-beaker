using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Monitoring;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Monitoring;

/// <summary>
/// 리소스 사용 이력 버퍼 테스트.
/// 이 버퍼가 도입되기 전에는 Docker·Node·Python 환경의 GetUsageHistoryAsync가
/// 모두 빈 리스트를 반환해, 호출자가 "이력이 없음"과 "이력 기능이 없음"을 구분할 수 없었다.
/// </summary>
public sealed class ResourceUsageHistoryTests
{
    private static ResourceUsage Usage(long bytes) => new() { MemoryUsageBytes = bytes };

    [Fact]
    public void Recent_ShouldReturnEmpty_WhenNothingRecorded()
    {
        new ResourceUsageHistory().Recent(10).Should().BeEmpty();
    }

    [Fact]
    public void Recent_ShouldReturnSnapshotsOldestFirst()
    {
        var history = new ResourceUsageHistory();
        history.Record(Usage(1));
        history.Record(Usage(2));
        history.Record(Usage(3));

        history.Recent(10).Select(u => u.MemoryUsageBytes).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Recent_ShouldReturnOnlyTheRequestedTail()
    {
        var history = new ResourceUsageHistory();
        for (var i = 1; i <= 5; i++)
        {
            history.Record(Usage(i));
        }

        history.Recent(2).Select(u => u.MemoryUsageBytes).Should().Equal(4, 5);
    }

    [Fact]
    public void Record_ShouldEvictOldestSnapshots_WhenCapacityIsExceeded()
    {
        var history = new ResourceUsageHistory(capacity: 3);
        for (var i = 1; i <= 5; i++)
        {
            history.Record(Usage(i));
        }

        history.Recent(100).Select(u => u.MemoryUsageBytes).Should().Equal(3, 4, 5);
    }

    [Fact]
    public void Recent_ShouldReturnEmpty_ForNonPositiveCount()
    {
        var history = new ResourceUsageHistory();
        history.Record(Usage(1));

        history.Recent(0).Should().BeEmpty();
        history.Recent(-1).Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldRejectNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceUsageHistory(0));
    }

    [Fact]
    public void Record_ShouldBeSafeUnderConcurrentWriters()
    {
        // 백그라운드 모니터링이 기록하는 동안 다른 스레드가 조회할 수 있다.
        var history = new ResourceUsageHistory(capacity: 50);

        Parallel.For(0, 500, i =>
        {
            history.Record(Usage(i));
            history.Recent(10);
        });

        history.Recent(1000).Should().HaveCount(50);
    }

    [Fact]
    public async Task EnvironmentResourceMonitor_ShouldAccumulateHistoryAcrossPolls()
    {
        var environment = new Mock<IExecutionEnvironment>();
        var sample = 0L;
        environment
            .Setup(e => e.GetResourceUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Usage(++sample));

        var monitor = new EnvironmentResourceMonitor(environment.Object, maxHistorySize: 2);

        await monitor.GetCurrentUsageAsync();
        await monitor.GetCurrentUsageAsync();
        await monitor.GetCurrentUsageAsync();

        var history = await monitor.GetUsageHistoryAsync(10);
        history.Select(u => u.MemoryUsageBytes).Should().Equal(2, 3);
    }
}

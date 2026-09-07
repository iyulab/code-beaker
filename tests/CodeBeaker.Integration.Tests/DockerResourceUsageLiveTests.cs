using CodeBeaker.Core.Interfaces;
using CodeBeaker.Integration.Tests.TestHelpers;
using CodeBeaker.Runtimes.Docker;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// 컨테이너의 리소스 사용량이 살아있는 데몬에서 실제로 읽히는지 확인한다.
///
/// 이 경로는 Docker 데몬의 stats 응답을 그대로 옮겨 담기만 하므로 mock 으로는
/// 아무것도 증명되지 않는다 — 응답 형태가 바뀌거나 필드가 비어 오면 조용히 0 이
/// 되는데, 그 0 은 "사용량 없음" 과 구분되지 않는다. 리소스 상한 강제와 사용 이력이
/// 모두 이 값을 읽으므로 그 침묵은 감지되지 않는 고장이 된다.
/// </summary>
public sealed class DockerResourceUsageLiveTests
{
    private static async Task SkipIfDockerImagesUnavailableAsync()
    {
        var reason = await DockerTestHelper.GetSkipReasonAsync();
        Skip.If(reason is not null, reason);
    }

    private static async Task WithMonitorAsync(Func<IResourceMonitor, Task> assert)
    {
        var runtime = new DockerRuntime();
        var environment = await runtime.CreateEnvironmentAsync(
            new RuntimeConfig { Environment = "python" });

        await using (environment)
        {
            var monitor = Assert.IsAssignableFrom<IResourceMonitor>(environment);
            await assert(monitor);
        }
    }

    [SkippableFact]
    public async Task GetCurrentUsage_ReportsLiveMemoryAndCpuCounters()
    {
        await SkipIfDockerImagesUnavailableAsync();

        await WithMonitorAsync(async monitor =>
        {
            var usage = await monitor.GetCurrentUsageAsync();

            // 살아있는 컨테이너는 메모리를 쓰고 CPU 시간을 소비했다. 둘 중 하나라도 0 이면
            // stats 응답을 읽지 못한 것이다(빈 ResourceUsage 가 그 실패의 모습이다).
            Assert.True(usage.MemoryUsageBytes > 0, $"memory was {usage.MemoryUsageBytes} bytes");
            Assert.True(usage.CpuUsageNanoseconds > 0, $"cpu was {usage.CpuUsageNanoseconds} ns");

            // 사용률은 사용량/상한에서 계산된다 — 상한을 못 읽으면 0 으로 남는다.
            Assert.InRange(usage.MemoryUsagePercent, double.Epsilon, 1.0);
            Assert.InRange(usage.CpuUsagePercent, 0.0, double.MaxValue);
            Assert.NotEqual(default, usage.Timestamp);
        });
    }

    [SkippableFact]
    public async Task GetCurrentUsage_RecordsEverySnapshotInHistory()
    {
        await SkipIfDockerImagesUnavailableAsync();

        await WithMonitorAsync(async monitor =>
        {
            await monitor.GetCurrentUsageAsync();
            await monitor.GetCurrentUsageAsync();

            var history = await monitor.GetUsageHistoryAsync();

            Assert.Equal(2, history.Count);
            Assert.All(history, snapshot => Assert.True(snapshot.MemoryUsageBytes > 0));
        });
    }

    [SkippableFact]
    public async Task CheckViolations_ReportsTheMemoryLimitBreachItReadsFromTheContainer()
    {
        await SkipIfDockerImagesUnavailableAsync();

        await WithMonitorAsync(async monitor =>
        {
            // 상한을 실제 사용량 아래로 두면 위반이 나와야 한다. 반대로 후한 상한에 대고
            // "위반 없음"을 확인하는 검사는 사용량을 아예 못 읽을 때도 통과하므로
            // 이 경로에 대해서는 아무것도 증명하지 못한다.
            var violation = await monitor.CheckViolationsAsync(
                new ResourceLimits { MemoryLimitBytes = 1 });

            Assert.NotNull(violation);
            Assert.Equal(ResourceViolationType.MemoryLimit, violation.Type);
            Assert.True(violation.CurrentValue > violation.LimitValue);
            Assert.True(violation.ShouldTerminate);
        });
    }

    [SkippableFact]
    public async Task CheckViolations_StaysSilentWhileUsageIsUnderTheLimit()
    {
        await SkipIfDockerImagesUnavailableAsync();

        await WithMonitorAsync(async monitor =>
        {
            var violation = await monitor.CheckViolationsAsync(
                new ResourceLimits { MemoryLimitBytes = 4L * 1024 * 1024 * 1024 });

            Assert.Null(violation);
        });
    }
}

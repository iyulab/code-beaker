using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Runtime;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Docker;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// RuntimeSelector 성능 벤치마크
/// 자동 런타임 선택 알고리즘의 성능과 정확도 측정
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class RuntimeSelectorBenchmarks
{
    private RuntimeSelector? _selector;
    private List<IExecutionRuntime>? _runtimes;

    [GlobalSetup]
    public void Setup()
    {
        _runtimes = new List<IExecutionRuntime>
        {
            new DockerRuntime(),
            new DenoRuntime(),
            new BunRuntime()
        };
        _selector = new RuntimeSelector(_runtimes);
    }

    [Benchmark(Description = "Select Best Runtime: Speed Preference")]
    public async Task SelectBestRuntime_Speed()
    {
        if (_selector == null) return;

        await _selector.SelectBestRuntimeAsync(
            "javascript",
            RuntimePreference.Speed);
    }

    [Benchmark(Description = "Select Best Runtime: Security Preference")]
    public async Task SelectBestRuntime_Security()
    {
        if (_selector == null) return;

        await _selector.SelectBestRuntimeAsync(
            "python",
            RuntimePreference.Security);
    }

    [Benchmark(Description = "Select Best Runtime: Memory Preference")]
    public async Task SelectBestRuntime_Memory()
    {
        if (_selector == null) return;

        await _selector.SelectBestRuntimeAsync(
            "typescript",
            RuntimePreference.Memory);
    }

    [Benchmark(Description = "Select Best Runtime: Balanced Preference")]
    public async Task SelectBestRuntime_Balanced()
    {
        if (_selector == null) return;

        await _selector.SelectBestRuntimeAsync(
            "javascript",
            RuntimePreference.Balanced);
    }

    [Benchmark(Description = "Select By Type: Docker")]
    public async Task SelectByType_Docker()
    {
        if (_selector == null) return;

        await _selector.SelectByTypeAsync(
            RuntimeType.Docker,
            "python");
    }

    [Benchmark(Description = "Select By Type: Deno")]
    public async Task SelectByType_Deno()
    {
        if (_selector == null) return;

        await _selector.SelectByTypeAsync(
            RuntimeType.Deno,
            "typescript");
    }

    [Benchmark(Description = "Select By Type: Bun")]
    public async Task SelectByType_Bun()
    {
        if (_selector == null) return;

        await _selector.SelectByTypeAsync(
            RuntimeType.Bun,
            "javascript");
    }

    [Benchmark(Description = "Get All Available Runtimes")]
    public async Task GetAllAvailableRuntimes()
    {
        if (_runtimes == null) return;

        var tasks = _runtimes.Select(r => r.IsAvailableAsync());
        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Calculate Selection Scores (All Preferences)")]
    public void CalculateAllScores()
    {
        if (_runtimes == null) return;

        foreach (var runtime in _runtimes)
        {
            var capabilities = runtime.GetCapabilities();

            // Simulate score calculation for all preferences
            _ = CalculateScore(capabilities, RuntimePreference.Speed);
            _ = CalculateScore(capabilities, RuntimePreference.Security);
            _ = CalculateScore(capabilities, RuntimePreference.Memory);
            _ = CalculateScore(capabilities, RuntimePreference.Balanced);
        }
    }

    // Simplified score calculation (mirrors RuntimeSelector logic)
    private double CalculateScore(RuntimeCapabilities cap, RuntimePreference? pref)
    {
        return pref switch
        {
            RuntimePreference.Speed => 10000.0 / cap.StartupTimeMs,
            RuntimePreference.Security => cap.IsolationLevel * 10.0,
            RuntimePreference.Memory => 1000.0 / cap.MemoryOverheadMB,
            _ => (10000.0 / cap.StartupTimeMs) * 0.4 +
                 (cap.IsolationLevel * 10.0) * 0.3 +
                 (1000.0 / cap.MemoryOverheadMB) * 0.3
        };
    }
}

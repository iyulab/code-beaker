using BenchmarkDotNet.Attributes;
using CodeBeaker.Core.Storage;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// FileStorage 인프라 성능 벤치마크
/// 저장소 읽기/쓰기 처리량 및 지연시간 측정
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class StorageBenchmarks : IDisposable
{
    private string _testDir = null!;
    private FileStorage _storage = null!;
    private const string SampleOutput = "Sample output from code execution";
    private const string SampleError = "";

    [GlobalSetup]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"codebeaker_bench_{Guid.NewGuid()}");
        _storage = new FileStorage(Path.Combine(_testDir, "storage"));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public void Dispose()
    {
        Cleanup();
    }

    [Benchmark(Description = "Save single result")]
    public async Task SaveResult_Single()
    {
        var executionId = Guid.NewGuid().ToString();
        await _storage.SaveResultAsync(
            executionId,
            stdout: SampleOutput,
            stderr: SampleError,
            exitCode: 0,
            durationMs: 100);
    }

    [Benchmark(Description = "Save and retrieve single result")]
    public async Task<Core.Models.ExecutionResult?> SaveAndRetrieve_Single()
    {
        var executionId = Guid.NewGuid().ToString();
        await _storage.SaveResultAsync(
            executionId,
            stdout: SampleOutput,
            stderr: SampleError,
            exitCode: 0,
            durationMs: 100);
        return await _storage.GetResultAsync(executionId);
    }

    [Benchmark(Description = "Save 10 results")]
    public async Task SaveResults_Batch10()
    {
        for (int i = 0; i < 10; i++)
        {
            var executionId = Guid.NewGuid().ToString();
            await _storage.SaveResultAsync(
                executionId,
                stdout: $"Output {i}",
                stderr: string.Empty,
                exitCode: 0,
                durationMs: 100);
        }
    }

    [Benchmark(Description = "Save 100 results")]
    public async Task SaveResults_Batch100()
    {
        for (int i = 0; i < 100; i++)
        {
            var executionId = Guid.NewGuid().ToString();
            await _storage.SaveResultAsync(
                executionId,
                stdout: $"Output {i}",
                stderr: string.Empty,
                exitCode: 0,
                durationMs: 100);
        }
    }
}

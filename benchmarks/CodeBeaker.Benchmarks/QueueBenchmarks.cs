using BenchmarkDotNet.Attributes;
using CodeBeaker.Core.Models;
using CodeBeaker.Core.Queue;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// FileQueue 인프라 성능 벤치마크
/// 큐 처리량, 지연시간, 동시성 성능 측정
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class QueueBenchmarks : IDisposable
{
    private string _testDir = null!;
    private FileQueue _queue = null!;

    [GlobalSetup]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"codebeaker_bench_{Guid.NewGuid()}");
        _queue = new FileQueue(Path.Combine(_testDir, "queue"));
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

    [Benchmark(Description = "Submit single task")]
    public async Task<string> SubmitTask_Single()
    {
        return await _queue.SubmitTaskAsync("print('test')", "python", new ExecutionConfig());
    }

    [Benchmark(Description = "Submit 10 tasks sequentially")]
    public async Task SubmitTasks_Sequential10()
    {
        for (int i = 0; i < 10; i++)
        {
            await _queue.SubmitTaskAsync($"print('{i}')", "python", new ExecutionConfig());
        }
    }

    [Benchmark(Description = "Submit 100 tasks sequentially")]
    public async Task SubmitTasks_Sequential100()
    {
        for (int i = 0; i < 100; i++)
        {
            await _queue.SubmitTaskAsync($"print('{i}')", "python", new ExecutionConfig());
        }
    }

    [Benchmark(Description = "Submit and retrieve single task")]
    public async Task<TaskItem?> SubmitAndRetrieve_Single()
    {
        await _queue.SubmitTaskAsync("print('test')", "python", new ExecutionConfig());
        return await _queue.GetTaskAsync(timeout: 1);
    }

    [Benchmark(Description = "Full cycle: submit, retrieve, complete")]
    public async Task FullCycle_Single()
    {
        var id = await _queue.SubmitTaskAsync("print('test')", "python", new ExecutionConfig());
        var task = await _queue.GetTaskAsync(timeout: 1);
        if (task != null)
        {
            await _queue.CompleteTaskAsync(task.ExecutionId);
        }
    }

    [Benchmark(Description = "Full cycle: 10 tasks")]
    public async Task FullCycle_Batch10()
    {
        // Submit batch
        var ids = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            ids.Add(await _queue.SubmitTaskAsync($"print('{i}')", "python", new ExecutionConfig()));
        }

        // Process batch
        for (int i = 0; i < 10; i++)
        {
            var task = await _queue.GetTaskAsync(timeout: 1);
            if (task != null)
            {
                await _queue.CompleteTaskAsync(task.ExecutionId);
            }
        }
    }
}

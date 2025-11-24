using System.Diagnostics;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Runtime;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Docker;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// 간단한 성능 측정 도구
/// BenchmarkDotNet 대신 직접 측정
/// </summary>
public class SimplePerformanceTest
{
    public static async Task MainPerf(string[] args)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("CodeBeaker Runtime Performance Comparison");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var runtimes = new List<(string Name, IExecutionRuntime Runtime)>
        {
            ("Docker", new DockerRuntime()),
            ("Deno", new DenoRuntime()),
            ("Bun", new BunRuntime())
        };

        // Check availability
        Console.WriteLine("Runtime Availability Check:");
        Console.WriteLine("-".PadRight(80, '-'));
        var availableRuntimes = new List<(string Name, IExecutionRuntime Runtime)>();

        foreach (var (name, runtime) in runtimes)
        {
            var isAvailable = await runtime.IsAvailableAsync();
            Console.WriteLine($"{name,-15} {(isAvailable ? "✅ Available" : "❌ Not Available")}");

            if (isAvailable)
            {
                availableRuntimes.Add((name, runtime));
                var caps = runtime.GetCapabilities();
                Console.WriteLine($"  Capabilities: Startup={caps.StartupTimeMs}ms, Memory={caps.MemoryOverheadMB}MB, Isolation={caps.IsolationLevel}/10");
            }
        }
        Console.WriteLine();

        if (availableRuntimes.Count == 0)
        {
            Console.WriteLine("No runtimes available for testing!");
            return;
        }

        // Test 1: Environment Creation Speed
        Console.WriteLine("Test 1: Environment Creation Speed");
        Console.WriteLine("-".PadRight(80, '-'));
        foreach (var (name, runtime) in availableRuntimes)
        {
            await TestEnvironmentCreation(name, runtime);
        }
        Console.WriteLine();

        // Test 2: Code Execution Speed
        Console.WriteLine("Test 2: Code Execution Speed");
        Console.WriteLine("-".PadRight(80, '-'));
        foreach (var (name, runtime) in availableRuntimes)
        {
            await TestCodeExecution(name, runtime);
        }
        Console.WriteLine();

        // Test 3: File Operations Speed
        Console.WriteLine("Test 3: File Operations Speed");
        Console.WriteLine("-".PadRight(80, '-'));
        foreach (var (name, runtime) in availableRuntimes)
        {
            await TestFileOperations(name, runtime);
        }
        Console.WriteLine();

        // Test 4: RuntimeSelector Performance
        Console.WriteLine("Test 4: RuntimeSelector Performance");
        Console.WriteLine("-".PadRight(80, '-'));
        await TestRuntimeSelector(availableRuntimes.Select(x => x.Runtime).ToList());
        Console.WriteLine();

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Performance Testing Complete");
        Console.WriteLine("=".PadRight(80, '='));
    }

    static async Task TestEnvironmentCreation(string name, IExecutionRuntime runtime)
    {
        var iterations = 3;
        var times = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var config = new RuntimeConfig
            {
                Environment = name == "Docker" ? "python" : name.ToLower(),
                WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"perf-{name.ToLower()}-{Guid.NewGuid():N}")
            };

            try
            {
                var env = await runtime.CreateEnvironmentAsync(config);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
                await env.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(config.WorkspaceDirectory))
                {
                    Directory.Delete(config.WorkspaceDirectory, true);
                }
            }
        }

        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();
        Console.WriteLine($"{name,-15} Avg: {avg,6:F1}ms  Min: {min,6}ms  Max: {max,6}ms");
    }

    static async Task TestCodeExecution(string name, IExecutionRuntime runtime)
    {
        var iterations = 5;
        var times = new List<long>();

        var config = new RuntimeConfig
        {
            Environment = name == "Docker" ? "python" : name.ToLower(),
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"perf-exec-{name.ToLower()}-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await runtime.CreateEnvironmentAsync(config);

            for (int i = 0; i < iterations; i++)
            {
                var command = new ExecuteCodeCommand
                {
                    Code = name == "Docker" ?
                        "print('test')" :
                        "console.log('test');"
                };

                var sw = Stopwatch.StartNew();
                await env.ExecuteAsync(command);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
            }

            await env.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }

        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();
        Console.WriteLine($"{name,-15} Avg: {avg,6:F1}ms  Min: {min,6}ms  Max: {max,6}ms");
    }

    static async Task TestFileOperations(string name, IExecutionRuntime runtime)
    {
        var iterations = 5;
        var times = new List<long>();

        var config = new RuntimeConfig
        {
            Environment = name == "Docker" ? "python" : name.ToLower(),
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"perf-file-{name.ToLower()}-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await runtime.CreateEnvironmentAsync(config);

            for (int i = 0; i < iterations; i++)
            {
                var writeCmd = new WriteFileCommand
                {
                    Path = name == "Docker" ? "/workspace/test.txt" : "test.txt",
                    Content = "Performance test data",
                    Mode = FileWriteMode.Create
                };

                var readCmd = new ReadFileCommand
                {
                    Path = name == "Docker" ? "/workspace/test.txt" : "test.txt"
                };

                var sw = Stopwatch.StartNew();
                await env.ExecuteAsync(writeCmd);
                await env.ExecuteAsync(readCmd);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
            }

            await env.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(config.WorkspaceDirectory))
            {
                Directory.Delete(config.WorkspaceDirectory, true);
            }
        }

        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();
        Console.WriteLine($"{name,-15} Avg: {avg,6:F1}ms  Min: {min,6}ms  Max: {max,6}ms");
    }

    static async Task TestRuntimeSelector(List<IExecutionRuntime> runtimes)
    {
        var selector = new RuntimeSelector(runtimes);
        var preferences = new[] { RuntimePreference.Speed, RuntimePreference.Security, RuntimePreference.Memory, RuntimePreference.Balanced };

        foreach (var pref in preferences)
        {
            var times = new List<long>();

            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                await selector.SelectBestRuntimeAsync("javascript", pref);
                sw.Stop();
                times.Add(sw.ElapsedTicks);
            }

            var avgMicroseconds = times.Average() / (Stopwatch.Frequency / 1000000.0);
            Console.WriteLine($"{pref,-15} Avg: {avgMicroseconds,6:F2}μs (100 iterations)");
        }
    }
}

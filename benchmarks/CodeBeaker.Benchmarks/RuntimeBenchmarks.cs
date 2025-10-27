using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Runtimes.Bun;
using CodeBeaker.Runtimes.Deno;
using CodeBeaker.Runtimes.Docker;

namespace CodeBeaker.Benchmarks;

/// <summary>
/// 런타임 성능 벤치마크
/// Docker, Deno, Bun 런타임의 시작 시간, 메모리, 처리량을 비교
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class RuntimeBenchmarks
{
    private IExecutionRuntime? _dockerRuntime;
    private IExecutionRuntime? _denoRuntime;
    private IExecutionRuntime? _bunRuntime;

    [GlobalSetup]
    public void Setup()
    {
        _dockerRuntime = new DockerRuntime();
        _denoRuntime = new DenoRuntime();
        _bunRuntime = new BunRuntime();
    }

    #region Startup Time Benchmarks

    [Benchmark(Description = "Docker: Environment Creation")]
    public async Task Docker_CreateEnvironment()
    {
        if (_dockerRuntime == null || !await _dockerRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-docker-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _dockerRuntime.CreateEnvironmentAsync(config);
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

    [Benchmark(Description = "Deno: Environment Creation")]
    public async Task Deno_CreateEnvironment()
    {
        if (_denoRuntime == null || !await _denoRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-deno-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _denoRuntime.CreateEnvironmentAsync(config);
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

    [Benchmark(Description = "Bun: Environment Creation")]
    public async Task Bun_CreateEnvironment()
    {
        if (_bunRuntime == null || !await _bunRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-bun-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _bunRuntime.CreateEnvironmentAsync(config);
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

    #endregion

    #region Code Execution Benchmarks

    [Benchmark(Description = "Docker: Execute Simple Code")]
    public async Task Docker_ExecuteCode()
    {
        if (_dockerRuntime == null || !await _dockerRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-docker-exec-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _dockerRuntime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "print('Hello from benchmark')"
            };

            await env.ExecuteAsync(command);
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

    [Benchmark(Description = "Deno: Execute Simple Code")]
    public async Task Deno_ExecuteCode()
    {
        if (_denoRuntime == null || !await _denoRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-deno-exec-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _denoRuntime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "console.log('Hello from benchmark');"
            };

            await env.ExecuteAsync(command);
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

    [Benchmark(Description = "Bun: Execute Simple Code")]
    public async Task Bun_ExecuteCode()
    {
        if (_bunRuntime == null || !await _bunRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-bun-exec-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _bunRuntime.CreateEnvironmentAsync(config);

            var command = new ExecuteCodeCommand
            {
                Code = "console.log('Hello from benchmark');"
            };

            await env.ExecuteAsync(command);
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

    #endregion

    #region File Operations Benchmarks

    [Benchmark(Description = "Docker: File Write & Read")]
    public async Task Docker_FileOperations()
    {
        if (_dockerRuntime == null || !await _dockerRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "python",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-docker-file-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _dockerRuntime.CreateEnvironmentAsync(config);

            var writeCmd = new WriteFileCommand
            {
                Path = "/workspace/test.txt",
                Content = "Benchmark test data",
                Mode = FileWriteMode.Create
            };

            await env.ExecuteAsync(writeCmd);

            var readCmd = new ReadFileCommand
            {
                Path = "/workspace/test.txt"
            };

            await env.ExecuteAsync(readCmd);
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

    [Benchmark(Description = "Deno: File Write & Read")]
    public async Task Deno_FileOperations()
    {
        if (_denoRuntime == null || !await _denoRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "deno",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-deno-file-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _denoRuntime.CreateEnvironmentAsync(config);

            var writeCmd = new WriteFileCommand
            {
                Path = "test.txt",
                Content = "Benchmark test data",
                Mode = FileWriteMode.Create
            };

            await env.ExecuteAsync(writeCmd);

            var readCmd = new ReadFileCommand
            {
                Path = "test.txt"
            };

            await env.ExecuteAsync(readCmd);
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

    [Benchmark(Description = "Bun: File Write & Read")]
    public async Task Bun_FileOperations()
    {
        if (_bunRuntime == null || !await _bunRuntime.IsAvailableAsync())
        {
            return;
        }

        var config = new RuntimeConfig
        {
            Environment = "bun",
            WorkspaceDirectory = Path.Combine(Path.GetTempPath(), $"bench-bun-file-{Guid.NewGuid():N}")
        };

        try
        {
            var env = await _bunRuntime.CreateEnvironmentAsync(config);

            var writeCmd = new WriteFileCommand
            {
                Path = "test.txt",
                Content = "Benchmark test data",
                Mode = FileWriteMode.Create
            };

            await env.ExecuteAsync(writeCmd);

            var readCmd = new ReadFileCommand
            {
                Path = "test.txt"
            };

            await env.ExecuteAsync(readCmd);
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

    #endregion
}

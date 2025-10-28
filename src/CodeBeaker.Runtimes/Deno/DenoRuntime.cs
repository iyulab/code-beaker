using System.Diagnostics;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes.Deno;

/// <summary>
/// Deno 런타임 구현 (JavaScript/TypeScript 네이티브 지원)
/// </summary>
public sealed class DenoRuntime : IExecutionRuntime
{
    private string? _denoPath;

    public string Name => "deno";
    public RuntimeType Type => RuntimeType.Deno;
    public string[] SupportedEnvironments => new[] { "deno", "typescript", "javascript" };

    public RuntimeCapabilities GetCapabilities()
    {
        return new RuntimeCapabilities
        {
            StartupTimeMs = 80,
            MemoryOverheadMB = 30,
            IsolationLevel = 7, // 권한 기반 샌드박스
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 100
        };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var denoPath = GetDenoPath();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = denoPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                _denoPath = denoPath; // 캐시
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deno 실행 파일 경로 찾기 (Windows 환경 지원)
    /// </summary>
    private string GetDenoPath()
    {
        // 이미 찾은 경로가 있으면 재사용
        if (!string.IsNullOrEmpty(_denoPath))
        {
            return _denoPath;
        }

        // 1. PATH에서 찾기 (기본)
        if (TryFindDenoInPath(out var pathDeno))
        {
            return pathDeno;
        }

        // 2. Windows 환경: 일반적인 설치 위치 확인
        if (OperatingSystem.IsWindows())
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".deno", "bin", "deno.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "deno", "deno.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "deno", "deno.exe"),
                @"C:\Program Files\deno\deno.exe",
                @"C:\deno\deno.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        // 3. 기본값으로 "deno" 반환 (PATH에 있을 것으로 기대)
        return "deno";
    }

    private bool TryFindDenoInPath(out string denoPath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            denoPath = string.Empty;
            return false;
        }

        var paths = pathEnv.Split(Path.PathSeparator);
        var denoExecutable = OperatingSystem.IsWindows() ? "deno.exe" : "deno";

        foreach (var dir in paths)
        {
            try
            {
                var fullPath = Path.Combine(dir, denoExecutable);
                if (File.Exists(fullPath))
                {
                    denoPath = fullPath;
                    return true;
                }
            }
            catch
            {
                // 잘못된 경로 무시
            }
        }

        denoPath = string.Empty;
        return false;
    }

    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        // Deno 사용 가능 여부 확인
        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Deno is not installed or not available in PATH. " +
                "Install from: https://deno.land/");
        }

        // 작업 디렉토리 생성
        if (!Directory.Exists(config.WorkspaceDirectory))
        {
            Directory.CreateDirectory(config.WorkspaceDirectory);
        }

        var denoPath = GetDenoPath();
        var environment = new DenoEnvironment(config, denoPath);
        await environment.InitializeAsync(cancellationToken);

        return environment;
    }
}

/// <summary>
/// Deno 실행 환경
/// </summary>
public sealed class DenoEnvironment : IExecutionEnvironment
{
    private readonly RuntimeConfig _config;
    private readonly string _denoPath;
    private EnvironmentState _state;
    private readonly string _environmentId;

    public string EnvironmentId => _environmentId;
    public RuntimeType RuntimeType => RuntimeType.Deno;
    public EnvironmentState State => _state;

    public DenoEnvironment(RuntimeConfig config, string denoPath)
    {
        _config = config;
        _denoPath = denoPath;
        _environmentId = Guid.NewGuid().ToString("N")[..12];
        _state = EnvironmentState.Initializing;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _state = EnvironmentState.Ready;
        return Task.CompletedTask;
    }

    public async Task<CommandResult> ExecuteAsync(
        Command command,
        CancellationToken cancellationToken = default)
    {
        if (_state == EnvironmentState.Stopped)
        {
            throw new InvalidOperationException("Environment is stopped");
        }

        _state = EnvironmentState.Running;

        try
        {
            var result = command switch
            {
                ExecuteShellCommand shell => await ExecuteShellCommandAsync(shell, cancellationToken),
                WriteFileCommand write => await ExecuteWriteFileAsync(write, cancellationToken),
                ReadFileCommand read => await ExecuteReadFileAsync(read, cancellationToken),
                ExecuteCodeCommand code => await ExecuteCodeCommandAsync(code, cancellationToken),
                CreateDirectoryCommand createDir => await ExecuteCreateDirectoryAsync(createDir, cancellationToken),
                _ => throw new NotSupportedException($"Command type {command.Type} not supported")
            };

            _state = EnvironmentState.Idle;
            return result;
        }
        catch (Exception ex)
        {
            _state = EnvironmentState.Error;
            return new CommandResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<CommandResult> ExecuteCodeCommandAsync(
        ExecuteCodeCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        // 임시 파일에 코드 작성
        var tempFile = Path.Combine(_config.WorkspaceDirectory, $"temp_{Guid.NewGuid():N}.ts");
        await File.WriteAllTextAsync(tempFile, command.Code, cancellationToken);

        try
        {
            var process = new Process
            {
                StartInfo = CreateProcessStartInfo(tempFile)
            };

            var stdout = new List<string>();
            var stderr = new List<string>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) stdout.Add(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) stderr.Add(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Result = string.Join("\n", stdout),
                Error = stderr.Count > 0 ? string.Join("\n", stderr) : null,
                DurationMs = (int)duration
            };
        }
        finally
        {
            // 임시 파일 정리
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<CommandResult> ExecuteShellCommandAsync(
        ExecuteShellCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        var filePath = Path.Combine(_config.WorkspaceDirectory, command.CommandName);

        var process = new Process
        {
            StartInfo = CreateProcessStartInfo(filePath, command.Args)
        };

        var stdout = new List<string>();
        var stderr = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) stdout.Add(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) stderr.Add(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        return new CommandResult
        {
            Success = process.ExitCode == 0,
            Result = string.Join("\n", stdout),
            Error = stderr.Count > 0 ? string.Join("\n", stderr) : null,
            DurationMs = (int)duration
        };
    }

    private async Task<CommandResult> ExecuteWriteFileAsync(
        WriteFileCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var filePath = GetFullPath(command.Path);
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            switch (command.Mode)
            {
                case FileWriteMode.Create:
                case FileWriteMode.Overwrite:
                    await File.WriteAllTextAsync(filePath, command.Content, cancellationToken);
                    break;
                case FileWriteMode.Append:
                    await File.AppendAllTextAsync(filePath, command.Content, cancellationToken);
                    break;
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new CommandResult
            {
                Success = true,
                Result = $"File written: {command.Path}",
                DurationMs = (int)duration
            };
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)duration
            };
        }
    }

    private async Task<CommandResult> ExecuteReadFileAsync(
        ReadFileCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var filePath = GetFullPath(command.Path);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new CommandResult
            {
                Success = true,
                Result = content,
                DurationMs = (int)duration
            };
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)duration
            };
        }
    }

    private Task<CommandResult> ExecuteCreateDirectoryAsync(
        CreateDirectoryCommand command,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var dirPath = GetFullPath(command.Path);
            Directory.CreateDirectory(dirPath);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return Task.FromResult(new CommandResult
            {
                Success = true,
                Result = $"Directory created: {command.Path}",
                DurationMs = (int)duration
            });
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)duration
            });
        }
    }

    private ProcessStartInfo CreateProcessStartInfo(string scriptPath, List<string>? args = null)
    {
        var permissions = _config.Permissions ?? new PermissionSettings
        {
            AllowRead = new List<string> { _config.WorkspaceDirectory },
            AllowWrite = new List<string> { _config.WorkspaceDirectory },
            AllowNet = false,
            AllowEnv = false,
            AllowRun = false
        };

        var arguments = new List<string>
        {
            "run",
            "--no-prompt"
        };

        // 권한 설정
        foreach (var path in permissions.AllowRead)
        {
            arguments.Add($"--allow-read={path}");
        }
        foreach (var path in permissions.AllowWrite)
        {
            arguments.Add($"--allow-write={path}");
        }
        if (permissions.AllowNet)
        {
            arguments.Add("--allow-net");
        }
        if (permissions.AllowEnv)
        {
            arguments.Add("--allow-env");
        }
        if (permissions.AllowRun)
        {
            arguments.Add("--allow-run");
        }

        arguments.Add(scriptPath);

        if (args != null)
        {
            arguments.AddRange(args);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _denoPath,
            Arguments = string.Join(" ", arguments),
            WorkingDirectory = _config.WorkspaceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // 환경 변수 설정
        foreach (var (key, value) in _config.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        // Deno 캐시 디렉토리
        startInfo.Environment["DENO_DIR"] = Path.Combine(
            _config.WorkspaceDirectory,
            ".deno-cache");
        startInfo.Environment["NO_COLOR"] = "1";

        return startInfo;
    }

    private string GetFullPath(string path)
    {
        // Handle /workspace virtual path (Unix-style container path)
        if (path.StartsWith("/workspace/") || path.StartsWith("/workspace\\"))
        {
            var relativePath = path.Substring("/workspace/".Length);
            return Path.Combine(_config.WorkspaceDirectory, relativePath);
        }

        if (path == "/workspace")
        {
            return _config.WorkspaceDirectory;
        }

        // Handle rooted paths
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(_config.WorkspaceDirectory, path);
    }

    public Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _state = EnvironmentState.Stopped;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

    /// <summary>
    /// 네이티브 런타임 리소스 사용량 조회 (제한적)
    /// Process-based 런타임은 실행 시 임시 프로세스를 생성하므로 정확한 추적이 어려움
    /// </summary>
    public Task<ResourceUsage?> GetResourceUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 현재 .NET 프로세스 자체의 리소스만 조회 가능
            // Deno 프로세스는 ExecuteAsync 중에만 존재하므로 추적 불가
            using var currentProcess = Process.GetCurrentProcess();

            var usage = new ResourceUsage
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageBytes = currentProcess.WorkingSet64,
                MemoryPeakBytes = currentProcess.PeakWorkingSet64,
                MemoryUsagePercent = 0, // OS 전체 메모리 대비 계산 필요
                CpuUsageNanoseconds = currentProcess.TotalProcessorTime.Ticks * 100, // Ticks to nanoseconds
                CpuUsagePercent = 0, // 실시간 CPU% 계산 복잡
                CpuThrottledMicroseconds = 0, // Process API에서 제공 안함
                DiskReadBytes = 0, // Process API에서 제공 안함
                DiskWriteBytes = 0,
                DiskUsageBytes = 0,
                NetworkRxBytes = 0, // Process API에서 제공 안함
                NetworkTxBytes = 0,
                ProcessCount = 1, // 현재 프로세스만
                FileDescriptorCount = currentProcess.HandleCount
            };

            return Task.FromResult<ResourceUsage?>(usage);
        }
        catch
        {
            return Task.FromResult<ResourceUsage?>(null);
        }
    }
}

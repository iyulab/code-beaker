using System.Diagnostics;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes.Bun;

/// <summary>
/// Bun 런타임 구현 (초고속 JavaScript/TypeScript)
/// </summary>
public sealed class BunRuntime : IExecutionRuntime
{
    private string? _bunPath;

    public string Name => "bun";
    public RuntimeType Type => RuntimeType.Bun;
    public string[] SupportedEnvironments => new[] { "bun", "typescript", "javascript", "nodejs" };

    public RuntimeCapabilities GetCapabilities()
    {
        return new RuntimeCapabilities
        {
            StartupTimeMs = 50, // Bun은 매우 빠름
            MemoryOverheadMB = 25, // 매우 적은 메모리
            IsolationLevel = 7, // Deno와 유사한 권한 기반
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 150 // 경량이므로 많은 동시 실행 가능
        };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bunPath = GetBunPath();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bunPath,
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
                _bunPath = bunPath; // 캐시
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
    /// Bun 실행 파일 경로 찾기 (Windows/Linux/Mac 지원)
    /// </summary>
    private string GetBunPath()
    {
        // 이미 찾은 경로가 있으면 재사용
        if (!string.IsNullOrEmpty(_bunPath))
        {
            return _bunPath;
        }

        // 1. PATH에서 찾기 (기본)
        if (TryFindBunInPath(out var pathBun))
        {
            return pathBun;
        }

        // 2. Windows 환경: 일반적인 설치 위치 확인
        if (OperatingSystem.IsWindows())
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bun", "bin", "bun.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bun", "bun.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "bun", "bun.exe"),
                @"C:\Program Files\bun\bun.exe",
                @"C:\bun\bun.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        // 3. Linux/Mac: 일반적인 설치 위치
        else
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bun", "bin", "bun"),
                "/usr/local/bin/bun",
                "/usr/bin/bun"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        // 4. 기본값으로 "bun" 반환 (PATH에 있을 것으로 기대)
        return "bun";
    }

    private bool TryFindBunInPath(out string bunPath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            bunPath = string.Empty;
            return false;
        }

        var paths = pathEnv.Split(Path.PathSeparator);
        var bunExecutable = OperatingSystem.IsWindows() ? "bun.exe" : "bun";

        foreach (var dir in paths)
        {
            try
            {
                var fullPath = Path.Combine(dir, bunExecutable);
                if (File.Exists(fullPath))
                {
                    bunPath = fullPath;
                    return true;
                }
            }
            catch
            {
                // 잘못된 경로 무시
            }
        }

        bunPath = string.Empty;
        return false;
    }

    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        // Bun 사용 가능 여부 확인
        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Bun is not installed or not available in PATH. " +
                "Install from: https://bun.sh/");
        }

        // 작업 디렉토리 생성
        if (!Directory.Exists(config.WorkspaceDirectory))
        {
            Directory.CreateDirectory(config.WorkspaceDirectory);
        }

        var bunPath = GetBunPath();
        var environment = new BunEnvironment(config, bunPath);
        await environment.InitializeAsync(cancellationToken);

        return environment;
    }
}

/// <summary>
/// Bun 실행 환경
/// </summary>
public sealed class BunEnvironment : IExecutionEnvironment
{
    private readonly RuntimeConfig _config;
    private readonly string _bunPath;
    private EnvironmentState _state;
    private readonly string _environmentId;

    public string EnvironmentId => _environmentId;
    public RuntimeType RuntimeType => RuntimeType.Bun;
    public EnvironmentState State => _state;

    public BunEnvironment(RuntimeConfig config, string bunPath)
    {
        _config = config;
        _bunPath = bunPath;
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

        // 임시 파일에 코드 작성 (Bun은 TypeScript 네이티브 지원)
        var extension = command.Code.Contains("import") || command.Code.Contains("export") ? ".ts" : ".js";
        var tempFile = Path.Combine(_config.WorkspaceDirectory, $"temp_{Guid.NewGuid():N}{extension}");
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
        // Bun은 기본적으로 샌드박스가 없으므로 권한 관리는 OS 레벨에서 처리
        var arguments = new List<string> { "run", scriptPath };

        if (args != null)
        {
            arguments.AddRange(args);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _bunPath,
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

        // Bun 캐시 디렉토리
        startInfo.Environment["BUN_INSTALL"] = Path.Combine(
            _config.WorkspaceDirectory,
            ".bun-cache");

        return startInfo;
    }

    private string GetFullPath(string path)
    {
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
            // Bun 프로세스는 ExecuteAsync 중에만 존재하므로 추적 불가
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

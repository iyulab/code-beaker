using System.Diagnostics;
using System.Text;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes.Node;

/// <summary>
/// Node.js 런타임 구현 (JavaScript/TypeScript 실행)
/// Phase 9: Runtime Ecosystem Expansion
/// </summary>
public sealed class NodeRuntime : IExecutionRuntime
{
    private string? _nodePath;

    public string Name => "node";
    public RuntimeType Type => RuntimeType.NodeJs;
    public string[] SupportedEnvironments => new[] { "node", "nodejs", "javascript", "typescript" };

    public RuntimeCapabilities GetCapabilities()
    {
        return new RuntimeCapabilities
        {
            StartupTimeMs = 100, // Node.js 시작 시간
            MemoryOverheadMB = 40, // Node.js 메모리 오버헤드
            IsolationLevel = 5, // 프로세스 수준 격리
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 80
        };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nodePath = GetNodePath();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
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
                _nodePath = nodePath; // 캐시
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
    /// Node.js 실행 파일 경로 찾기
    /// </summary>
    private string GetNodePath()
    {
        // 이미 찾은 경로가 있으면 재사용
        if (!string.IsNullOrEmpty(_nodePath))
        {
            return _nodePath;
        }

        // 1. PATH에서 찾기 (기본)
        if (TryFindNodeInPath(out var pathNode))
        {
            return pathNode;
        }

        // 2. Windows 환경: 일반적인 설치 위치 확인
        if (OperatingSystem.IsWindows())
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
                @"C:\Program Files\nodejs\node.exe",
                @"C:\Program Files (x86)\nodejs\node.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        else
        {
            // 3. Linux/Mac: 일반적인 설치 위치
            var possiblePaths = new[]
            {
                "/usr/bin/node",
                "/usr/local/bin/node",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nvm", "versions", "node")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        // PATH와 일반적인 위치에서 찾지 못하면 기본값
        return OperatingSystem.IsWindows() ? "node.exe" : "node";
    }

    private bool TryFindNodeInPath(out string nodePath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            nodePath = string.Empty;
            return false;
        }

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var nodeFileName = OperatingSystem.IsWindows() ? "node.exe" : "node";

        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), nodeFileName);
                if (File.Exists(fullPath))
                {
                    nodePath = fullPath;
                    return true;
                }
            }
            catch
            {
                // 잘못된 경로 무시
            }
        }

        nodePath = string.Empty;
        return false;
    }

    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        // Node.js 사용 가능 여부 확인
        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Node.js is not installed or not available in PATH. " +
                "Install from: https://nodejs.org/");
        }

        // 작업 디렉토리 생성
        if (!Directory.Exists(config.WorkspaceDirectory))
        {
            Directory.CreateDirectory(config.WorkspaceDirectory);
        }

        var nodePath = GetNodePath();
        var environment = new NodeEnvironment(config, nodePath);
        await environment.InitializeAsync(cancellationToken);

        return environment;
    }
}

/// <summary>
/// Node.js 실행 환경
/// Phase 9: 프로세스 기반 격리, IResourceMonitor 지원
/// </summary>
public sealed class NodeEnvironment : IExecutionEnvironment, IResourceMonitor
{
    private readonly RuntimeConfig _config;
    private readonly string _nodePath;
    private EnvironmentState _state;
    private readonly string _environmentId;
    private Process? _currentProcess;

    public string EnvironmentId => _environmentId;
    public RuntimeType RuntimeType => RuntimeType.NodeJs;
    public EnvironmentState State => _state;

    public NodeEnvironment(RuntimeConfig config, string nodePath)
    {
        _config = config;
        _nodePath = nodePath;
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
                ExecuteCodeCommand code => await ExecuteCodeAsync(code, cancellationToken),
                ExecuteShellCommand shell => await ExecuteShellAsync(shell, cancellationToken),
                InstallPackagesCommand install => await InstallPackagesAsync(install, cancellationToken),
                _ => throw new NotSupportedException($"Command type {command.Type} is not supported by Node.js runtime")
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
                Error = ex.Message,
                DurationMs = 0
            };
        }
    }

    private async Task<CommandResult> ExecuteCodeAsync(ExecuteCodeCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 코드를 임시 파일로 저장
            var tempFile = Path.Combine(_config.WorkspaceDirectory, $"code_{Guid.NewGuid():N}.js");
            await File.WriteAllTextAsync(tempFile, command.Code, cancellationToken);

            try
            {
                // Node.js 실행
                _currentProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _nodePath,
                        Arguments = $"\"{tempFile}\"",
                        WorkingDirectory = _config.WorkspaceDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                _currentProcess.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) outputBuilder.AppendLine(e.Data);
                };
                _currentProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) errorBuilder.AppendLine(e.Data);
                };

                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();

                // 타임아웃 적용
                var timeout = _config.ResourceLimits?.TimeoutSeconds ?? 300;
                var completed = await _currentProcess.WaitForExitAsync(
                    TimeSpan.FromSeconds(timeout),
                    cancellationToken);

                if (!completed)
                {
                    _currentProcess.Kill(true);
                    stopwatch.Stop();

                    return new CommandResult
                    {
                        Success = false,
                        Result = string.Empty,
                        Error = $"Execution timeout ({timeout}s)",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                stopwatch.Stop();

                var output = outputBuilder.ToString().TrimEnd();
                var error = errorBuilder.ToString().TrimEnd();

                return new CommandResult
                {
                    Success = _currentProcess.ExitCode == 0,
                    Result = output,
                    Error = string.IsNullOrEmpty(error) ? null : error,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                // 임시 파일 정리
                try { File.Delete(tempFile); } catch { }
                _currentProcess = null;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<CommandResult> ExecuteShellAsync(ExecuteShellCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command.CommandName,
                    Arguments = string.Join(" ", command.Args),
                    WorkingDirectory = _config.WorkspaceDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _currentProcess.Start();
            var output = await _currentProcess.StandardOutput.ReadToEndAsync();
            var error = await _currentProcess.StandardError.ReadToEndAsync();
            await _currentProcess.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();

            return new CommandResult
            {
                Success = _currentProcess.ExitCode == 0,
                Result = output,
                Error = string.IsNullOrEmpty(error) ? null : error,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            _currentProcess = null;
        }
    }

    private async Task<CommandResult> InstallPackagesAsync(InstallPackagesCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Determine npm path (usually in same directory as node)
            var npmPath = GetNpmPath();

            // Build npm install command
            var args = new List<string> { "install" };

            if (command.Global)
            {
                args.Add("--global");
            }

            // Add packages
            if (command.Packages != null && command.Packages.Count > 0)
            {
                args.AddRange(command.Packages);
            }

            // Add custom flags
            if (command.Flags != null && command.Flags.Count > 0)
            {
                args.AddRange(command.Flags);
            }

            // If requirements file specified, install from package.json
            if (!string.IsNullOrEmpty(command.RequirementsFile))
            {
                // npm install reads package.json by default
                // No additional args needed
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = _config.WorkspaceDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Longer timeout for package installation (default 600 seconds)
            var timeout = _config.ResourceLimits?.TimeoutSeconds ?? 600;
            var completed = await process.WaitForExitAsync(
                TimeSpan.FromSeconds(timeout),
                cancellationToken);

            if (!completed)
            {
                process.Kill(true);
                stopwatch.Stop();

                return new CommandResult
                {
                    Success = false,
                    Result = string.Empty,
                    Error = $"Package installation timeout ({timeout}s)",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            stopwatch.Stop();

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Result = output,
                Error = string.IsNullOrEmpty(error) ? null : error,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    private string GetNpmPath()
    {
        // npm is usually in the same directory as node
        var nodeDir = Path.GetDirectoryName(_nodePath);
        if (!string.IsNullOrEmpty(nodeDir))
        {
            var npmPath = Path.Combine(nodeDir, OperatingSystem.IsWindows() ? "npm.cmd" : "npm");
            if (File.Exists(npmPath))
            {
                return npmPath;
            }
        }

        // Fallback to PATH
        return OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
    }

    public Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _currentProcess?.Kill(true);
        _currentProcess?.Dispose();
        _currentProcess = null;
        _state = EnvironmentState.Stopped;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

    // Phase 9: IResourceMonitor 구현 (프로세스 기반)
    public Task<ResourceUsage> GetCurrentUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Refresh();

                return Task.FromResult(new ResourceUsage
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryUsageBytes = _currentProcess.WorkingSet64,
                    MemoryPeakBytes = _currentProcess.PeakWorkingSet64,
                    CpuUsageNanoseconds = _currentProcess.TotalProcessorTime.Ticks * 100,
                    ProcessCount = 1
                });
            }

            return Task.FromResult(new ResourceUsage());
        }
        catch
        {
            return Task.FromResult(new ResourceUsage());
        }
    }

    public Task<ResourceViolation?> CheckViolationsAsync(
        ResourceLimits limits,
        CancellationToken cancellationToken = default)
    {
        // 프로세스 기반 런타임에서는 제한 위반 검사 미지원
        return Task.FromResult<ResourceViolation?>(null);
    }

    public Task<List<ResourceUsage>> GetUsageHistoryAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        // 이력 저장 미지원
        return Task.FromResult(new List<ResourceUsage>());
    }

    public async Task<ResourceUsage?> GetResourceUsageAsync(CancellationToken cancellationToken = default)
    {
        var usage = await GetCurrentUsageAsync(cancellationToken);

        if (usage.MemoryUsageBytes == 0 && usage.CpuUsageNanoseconds == 0)
        {
            return null;
        }

        return usage;
    }
}

/// <summary>
/// Process 확장 메서드 (타임아웃 지원)
/// </summary>
internal static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(
        this Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

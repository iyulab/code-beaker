using System.Diagnostics;
using System.Text;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes.Python;

/// <summary>
/// Python 런타임 구현 (Python 3.x 실행)
/// Phase 9.2: Runtime Ecosystem Expansion
/// </summary>
public sealed class PythonRuntime : IExecutionRuntime
{
    private string? _pythonPath;

    public string Name => "python";
    public RuntimeType Type => RuntimeType.Python;
    public string[] SupportedEnvironments => new[] { "python", "python3", "py" };

    public RuntimeCapabilities GetCapabilities()
    {
        return new RuntimeCapabilities
        {
            StartupTimeMs = 150, // Python 시작 시간
            MemoryOverheadMB = 50, // Python 메모리 오버헤드
            IsolationLevel = 5, // 프로세스 수준 격리
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 60
        };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pythonPath = GetPythonPath();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
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
                _pythonPath = pythonPath; // 캐시
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
    /// Python 실행 파일 경로 찾기
    /// </summary>
    private string GetPythonPath()
    {
        // 이미 찾은 경로가 있으면 재사용
        if (!string.IsNullOrEmpty(_pythonPath))
        {
            return _pythonPath;
        }

        // 1. PATH에서 찾기 (기본)
        if (TryFindPythonInPath(out var pathPython))
        {
            return pathPython;
        }

        // 2. Windows 환경: 일반적인 설치 위치 확인
        if (OperatingSystem.IsWindows())
        {
            var possiblePaths = new[]
            {
                // Python.org 설치
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python310", "python.exe"),

                // AppData Roaming
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Python", "Python312", "python.exe"),

                // System-wide installations
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",

                // Anaconda
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "anaconda3", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "miniconda3", "python.exe")
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
                "/usr/bin/python3",
                "/usr/local/bin/python3",
                "/usr/bin/python",
                "/usr/local/bin/python",

                // pyenv
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pyenv", "shims", "python3"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pyenv", "shims", "python"),

                // Homebrew (macOS)
                "/opt/homebrew/bin/python3",
                "/usr/local/opt/python@3/bin/python3"
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
        return OperatingSystem.IsWindows() ? "python.exe" : "python3";
    }

    private bool TryFindPythonInPath(out string pythonPath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            pythonPath = string.Empty;
            return false;
        }

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        // Windows: python.exe 우선, python3.exe도 확인
        // Unix: python3 우선, python도 확인
        var pythonFileNames = OperatingSystem.IsWindows()
            ? new[] { "python.exe", "python3.exe" }
            : new[] { "python3", "python" };

        foreach (var fileName in pythonFileNames)
        {
            foreach (var path in paths)
            {
                try
                {
                    var fullPath = Path.Combine(path.Trim(), fileName);
                    if (File.Exists(fullPath))
                    {
                        pythonPath = fullPath;
                        return true;
                    }
                }
                catch
                {
                    // 잘못된 경로 무시
                }
            }
        }

        pythonPath = string.Empty;
        return false;
    }

    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        // Python 사용 가능 여부 확인
        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Python is not installed or not available in PATH. " +
                "Install from: https://www.python.org/downloads/");
        }

        // 작업 디렉토리 생성
        if (!Directory.Exists(config.WorkspaceDirectory))
        {
            Directory.CreateDirectory(config.WorkspaceDirectory);
        }

        var pythonPath = GetPythonPath();
        var environment = new PythonEnvironment(config, pythonPath);
        await environment.InitializeAsync(cancellationToken);

        return environment;
    }
}

/// <summary>
/// Python 실행 환경
/// Phase 9.2: 프로세스 기반 격리, IResourceMonitor 지원
/// </summary>
public sealed class PythonEnvironment : IExecutionEnvironment, IResourceMonitor
{
    private readonly RuntimeConfig _config;
    private readonly string _pythonPath;
    private EnvironmentState _state;
    private readonly string _environmentId;
    private Process? _currentProcess;
    private string? _venvPath;  // Phase 10: Virtual environment path
    private string? _venvPythonPath;  // Phase 10: Python path in venv

    public string EnvironmentId => _environmentId;
    public RuntimeType RuntimeType => RuntimeType.Python;
    public EnvironmentState State => _state;

    public PythonEnvironment(RuntimeConfig config, string pythonPath)
    {
        _config = config;
        _pythonPath = pythonPath;
        _environmentId = Guid.NewGuid().ToString("N")[..12];
        _state = EnvironmentState.Initializing;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Phase 10: Create virtual environment
        _venvPath = Path.Combine(_config.WorkspaceDirectory, "venv");

        try
        {
            // Create venv if it doesn't exist
            if (!Directory.Exists(_venvPath))
            {
                var venvProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"-m venv \"{_venvPath}\"",
                        WorkingDirectory = _config.WorkspaceDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                venvProcess.Start();
                await venvProcess.WaitForExitAsync(cancellationToken);

                if (venvProcess.ExitCode != 0)
                {
                    // If venv creation fails, use system Python
                    _venvPath = null;
                }
            }

            // Set venv Python path
            if (_venvPath != null && Directory.Exists(_venvPath))
            {
                _venvPythonPath = OperatingSystem.IsWindows()
                    ? Path.Combine(_venvPath, "Scripts", "python.exe")
                    : Path.Combine(_venvPath, "bin", "python");
            }
        }
        catch
        {
            // If venv setup fails, continue with system Python
            _venvPath = null;
            _venvPythonPath = null;
        }

        _state = EnvironmentState.Ready;
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
                _ => throw new NotSupportedException($"Command type {command.Type} is not supported by Python runtime")
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
            var tempFile = Path.Combine(_config.WorkspaceDirectory, $"code_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(tempFile, command.Code, cancellationToken);

            try
            {
                // Python 실행 (venv Python if available)
                var pythonExePath = _venvPythonPath ?? _pythonPath;
                _currentProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExePath,
                        Arguments = $"-u \"{tempFile}\"", // -u: unbuffered output
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
            // Determine pip path (use venv pip if available)
            var pipPath = GetPipPath();

            // Build pip install command
            var args = new List<string> { "install" };

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

            // If requirements file specified
            if (!string.IsNullOrEmpty(command.RequirementsFile))
            {
                var requirementsPath = Path.Combine(_config.WorkspaceDirectory, command.RequirementsFile);
                if (File.Exists(requirementsPath))
                {
                    args.Add("-r");
                    args.Add($"\"{requirementsPath}\"");
                }
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pipPath,
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

    private string GetPipPath()
    {
        // Use venv pip if available
        if (_venvPath != null && Directory.Exists(_venvPath))
        {
            var venvPipPath = OperatingSystem.IsWindows()
                ? Path.Combine(_venvPath, "Scripts", "pip.exe")
                : Path.Combine(_venvPath, "bin", "pip");

            if (File.Exists(venvPipPath))
            {
                return venvPipPath;
            }
        }

        // Fallback to system pip
        var pythonDir = Path.GetDirectoryName(_pythonPath);
        if (!string.IsNullOrEmpty(pythonDir))
        {
            var pipPath = Path.Combine(pythonDir, OperatingSystem.IsWindows() ? "pip.exe" : "pip");
            if (File.Exists(pipPath))
            {
                return pipPath;
            }

            // Try Scripts directory on Windows
            if (OperatingSystem.IsWindows())
            {
                var scriptsDir = Path.Combine(pythonDir, "Scripts");
                var scriptsPipPath = Path.Combine(scriptsDir, "pip.exe");
                if (File.Exists(scriptsPipPath))
                {
                    return scriptsPipPath;
                }
            }
        }

        // Fallback to PATH
        return OperatingSystem.IsWindows() ? "pip.exe" : "pip";
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

    // Phase 9.2: IResourceMonitor 구현 (프로세스 기반)
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

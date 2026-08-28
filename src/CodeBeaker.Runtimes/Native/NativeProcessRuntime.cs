using System.Diagnostics;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;

namespace CodeBeaker.Runtimes.Native;

/// <summary>
/// Runs shell commands as plain OS processes, with no language-specific interpreter of
/// its own. Fills <see cref="RuntimeType.NativeProcess"/>, which was declared on the
/// <see cref="RuntimeType"/> enum but had no implementing class — every other language
/// runtime here (Deno/Bun/Node/Python) requires that language's toolchain to be
/// installed, so a repository whose build/test tooling isn't one of those (dotnet, go,
/// java, rust, ...) could never get a session at all.
///
/// This runtime has no sandboxing of its own — it is exactly as isolated as the OS
/// process it spawns, no more. <see cref="RuntimeCapabilities.IsolationLevel"/> reports
/// that honestly (lowest of all registered runtimes) so <c>RuntimeSelector</c>'s
/// <c>Security</c>/<c>Balanced</c> preferences naturally prefer a stronger runtime
/// (e.g. Docker) when one supports the requested environment.
/// </summary>
public sealed class NativeProcessRuntime : IExecutionRuntime
{
    public string Name => "native";
    public RuntimeType Type => RuntimeType.NativeProcess;

    /// <summary>
    /// Environments not already served by a dedicated runtime here (Deno/Bun/Node/Python) —
    /// mirrors <c>RuntimeRegistry</c>'s existing "csharp"/"cs"/"dotnet",
    /// "go"/"golang" naming so callers already familiar with that registry recognize these.
    /// </summary>
    public string[] SupportedEnvironments =>
        ["dotnet", "csharp", "cs", "go", "golang", "java", "rust", "native"];

    public RuntimeCapabilities GetCapabilities() => new()
    {
        StartupTimeMs = 10,
        MemoryOverheadMB = 5,
        IsolationLevel = 1,
        SupportsFilesystemPersistence = true,
        SupportsNetworkAccess = true,
        MaxConcurrentExecutions = 100,
    };

    // No interpreter of its own to check for — OS process spawning is always available.
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(config.WorkspaceDirectory))
        {
            Directory.CreateDirectory(config.WorkspaceDirectory);
        }

        IExecutionEnvironment environment = new NativeProcessEnvironment(config);
        return Task.FromResult(environment);
    }
}

public sealed class NativeProcessEnvironment(RuntimeConfig config) : IExecutionEnvironment
{
    private readonly string _environmentId = Guid.NewGuid().ToString("N")[..12];
    private EnvironmentState _state = EnvironmentState.Ready;
    private Process? _currentProcess;

    public string EnvironmentId => _environmentId;
    public RuntimeType RuntimeType => RuntimeType.NativeProcess;
    public EnvironmentState State => _state;

    public async Task<CommandResult> ExecuteAsync(Command command, CancellationToken cancellationToken = default)
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
                ExecuteShellCommand shell => await ExecuteShellAsync(shell, cancellationToken),
                _ => throw new NotSupportedException(
                    $"Command type {command.Type} is not supported by the native process runtime — only shell commands run here"),
            };

            _state = EnvironmentState.Idle;
            return result;
        }
        catch (Exception ex)
        {
            _state = EnvironmentState.Error;
            return new CommandResult { Success = false, Error = ex.Message, DurationMs = 0 };
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
                    WorkingDirectory = command.WorkingDirectory ?? config.WorkspaceDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            if (command.Environment is not null)
            {
                foreach (var (key, value) in command.Environment)
                {
                    _currentProcess.StartInfo.EnvironmentVariables[key] = value;
                }
            }

            _currentProcess.Start();
            var timeout = config.ResourceLimits?.TimeoutSeconds ?? 300;
            var completed = await WaitForExitAsync(_currentProcess, TimeSpan.FromSeconds(timeout), cancellationToken);

            if (!completed)
            {
                _currentProcess.Kill(true);
                stopwatch.Stop();
                return new CommandResult
                {
                    Success = false,
                    Error = $"Execution timeout ({timeout}s)",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }

            var output = await _currentProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await _currentProcess.StandardError.ReadToEndAsync(cancellationToken);
            stopwatch.Stop();

            return new CommandResult
            {
                Success = _currentProcess.ExitCode == 0,
                Result = output,
                Error = string.IsNullOrEmpty(error) ? null : error,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CommandResult { Success = false, Error = ex.Message, DurationMs = (int)stopwatch.ElapsedMilliseconds };
        }
        finally
        {
            _currentProcess = null;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
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

    public Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _currentProcess?.Kill(true);
        _currentProcess?.Dispose();
        _currentProcess = null;
        _state = EnvironmentState.Stopped;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CleanupAsync();

    public Task<ResourceUsage?> GetResourceUsageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ResourceUsage?>(null);
}

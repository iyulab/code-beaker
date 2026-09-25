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
    /// uses the same "csharp"/"cs"/"dotnet", "go"/"golang" names as <c>LanguageCatalog</c>
    /// so a caller can pass either the canonical name or an alias.
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

    private Task<CommandResult> ExecuteShellAsync(ExecuteShellCommand command, CancellationToken cancellationToken) =>
        ShellProcess.RunAsync(
            command,
            config.WorkspaceDirectory,
            config.ResourceLimits?.TimeoutSeconds ?? ShellProcess.DefaultTimeoutSeconds,
            process => _currentProcess = process,
            cancellationToken);

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

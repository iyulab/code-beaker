using CodeBeaker.Commands;
using CodeBeaker.Commands.Models;
using CodeBeaker.Core.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CodeBeaker.Runtimes.Docker;

/// <summary>
/// Docker 컨테이너 기반 런타임
/// </summary>
public sealed class DockerRuntime : IExecutionRuntime
{
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;

    public DockerRuntime()
    {
        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _docker = new DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
        _commandExecutor = new CommandExecutor(_docker);
    }

    public string Name => "docker";

    public RuntimeType Type => RuntimeType.Docker;

    public string[] SupportedEnvironments => new[]
    {
        "python",
        "javascript",
        "nodejs",
        "go",
        "golang",
        "csharp",
        "dotnet"
    };

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _docker.System.PingAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IExecutionEnvironment> CreateEnvironmentAsync(
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new InvalidOperationException("Docker is not available");
        }

        var environment = new DockerEnvironment(_docker, _commandExecutor, config);
        await environment.InitializeAsync(cancellationToken);
        return environment;
    }

    public RuntimeCapabilities GetCapabilities()
    {
        return new RuntimeCapabilities
        {
            StartupTimeMs = 2000,
            MemoryOverheadMB = 250,
            IsolationLevel = 9,
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 50
        };
    }
}

/// <summary>
/// Docker 컨테이너 실행 환경
/// </summary>
internal sealed class DockerEnvironment : IExecutionEnvironment
{
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly RuntimeConfig _config;
    private string _containerId = string.Empty;

    public DockerEnvironment(
        DockerClient docker,
        CommandExecutor commandExecutor,
        RuntimeConfig config)
    {
        _docker = docker;
        _commandExecutor = commandExecutor;
        _config = config;
        EnvironmentId = Guid.NewGuid().ToString("N");
    }

    public string EnvironmentId { get; }

    public RuntimeType RuntimeType => RuntimeType.Docker;

    public EnvironmentState State { get; private set; } = EnvironmentState.Initializing;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Docker 이미지 결정
            var dockerImage = GetDefaultImage(_config.Environment);

            // 컨테이너 생성 (장기 실행)
            var createParams = new CreateContainerParameters
            {
                Image = dockerImage,
                Cmd = new[] { "sleep", "infinity" }, // Keep alive
                AttachStdout = true,
                AttachStderr = true,
                Tty = false,
                WorkingDir = "/workspace",
                Labels = new Dictionary<string, string>
                {
                    ["codebeaker.environment"] = EnvironmentId,
                    ["codebeaker.language"] = _config.Environment,
                    ["codebeaker.created"] = DateTime.UtcNow.ToString("o"),
                    ["codebeaker.runtime"] = "docker"
                },
                HostConfig = new HostConfig
                {
                    Memory = _config.ResourceLimits?.MemoryLimitMB.HasValue == true
                        ? _config.ResourceLimits.MemoryLimitMB.Value * 1024 * 1024
                        : 512 * 1024 * 1024,
                    CPUShares = _config.ResourceLimits?.CpuShares ?? 1024,
                    NetworkMode = "none",
                    AutoRemove = false
                }
            };

            var container = await _docker.Containers.CreateContainerAsync(createParams, cancellationToken);
            _containerId = container.ID;

            await _docker.Containers.StartContainerAsync(_containerId, new ContainerStartParameters(), cancellationToken);

            State = EnvironmentState.Ready;
        }
        catch (Exception)
        {
            State = EnvironmentState.Error;
            throw;
        }
    }

    public async Task<CommandResult> ExecuteAsync(
        Command command,
        CancellationToken cancellationToken = default)
    {
        if (State != EnvironmentState.Ready && State != EnvironmentState.Idle)
        {
            throw new InvalidOperationException($"Environment is not ready: {State}");
        }

        State = EnvironmentState.Running;

        try
        {
            var result = await _commandExecutor.ExecuteAsync(command, _containerId, cancellationToken);
            State = EnvironmentState.Idle;
            return result;
        }
        catch (Exception ex)
        {
            State = EnvironmentState.Error;
            return new CommandResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = 0
            };
        }
    }

    public async Task<EnvironmentState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            return EnvironmentState.Stopped;
        }

        try
        {
            var container = await _docker.Containers.InspectContainerAsync(_containerId, cancellationToken);

            if (container.State.Running)
            {
                return State; // 현재 상태 유지
            }
            else
            {
                return EnvironmentState.Stopped;
            }
        }
        catch
        {
            return EnvironmentState.Error;
        }
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            return;
        }

        try
        {
            await _docker.Containers.StopContainerAsync(
                _containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                cancellationToken);

            await _docker.Containers.RemoveContainerAsync(
                _containerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);

            State = EnvironmentState.Stopped;
        }
        catch
        {
            // 컨테이너가 이미 없을 수 있음
            State = EnvironmentState.Stopped;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

    private static string GetDefaultImage(string environment)
    {
        return environment.ToLowerInvariant() switch
        {
            "python" => "codebeaker-python:latest",
            "javascript" or "js" or "nodejs" => "codebeaker-nodejs:latest",
            "go" or "golang" => "codebeaker-golang:latest",
            "csharp" or "cs" or "dotnet" => "codebeaker-dotnet:latest",
            _ => throw new NotSupportedException($"Environment not supported: {environment}")
        };
    }
}

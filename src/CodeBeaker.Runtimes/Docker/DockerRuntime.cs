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

    /// <summary>
    /// 기존 Docker 컨테이너에 재연결 (Phase 6.3)
    /// </summary>
    public async Task<IExecutionEnvironment?> ReconnectEnvironmentAsync(
        string containerId,
        RuntimeConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 컨테이너 존재 여부 확인
            var container = await _docker.Containers.InspectContainerAsync(containerId, cancellationToken);

            if (container == null || !container.State.Running)
            {
                return null;
            }

            // 기존 컨테이너에 재연결
            var environment = new DockerEnvironment(_docker, _commandExecutor, config, containerId);

            return environment;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Docker 컨테이너 실행 환경 (Phase 8.1: IResourceMonitor 구현 추가)
/// </summary>
internal sealed class DockerEnvironment : IExecutionEnvironment, IResourceMonitor
{
    private readonly DockerClient _docker;
    private readonly CommandExecutor _commandExecutor;
    private readonly RuntimeConfig _config;
    private string _containerId = string.Empty;

    public DockerEnvironment(
        DockerClient docker,
        CommandExecutor commandExecutor,
        RuntimeConfig config,
        string? existingContainerId = null)
    {
        _docker = docker;
        _commandExecutor = commandExecutor;
        _config = config;
        EnvironmentId = Guid.NewGuid().ToString("N");

        // Phase 6.3: 기존 컨테이너 ID가 제공되면 재연결 모드
        if (!string.IsNullOrEmpty(existingContainerId))
        {
            _containerId = existingContainerId;
            State = EnvironmentState.Ready; // 이미 실행 중인 컨테이너
        }
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
                HostConfig = BuildHostConfig(_config.ResourceLimits)
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

    public async Task<ResourceUsage> GetCurrentUsageAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            return new ResourceUsage(); // 빈 객체 반환
        }

        try
        {
            // Docker stats API 호출 (stream = false로 단일 스냅샷만 요청)
            // 참고: GetContainerStatsAsync는 Stream을 반환하므로 JSON 파싱 필요
            using var statsStream = await _docker.Containers.GetContainerStatsAsync(
                _containerId,
                new ContainerStatsParameters { Stream = false },
                cancellationToken);

            // Stream에서 JSON 읽기 및 역직렬화
            using var reader = new StreamReader(statsStream);
            var jsonContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new ResourceUsage(); // 빈 객체 반환
            }

            var stats = System.Text.Json.JsonSerializer.Deserialize<ContainerStatsResponse>(
                jsonContent,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (stats == null)
            {
                return new ResourceUsage(); // 빈 객체 반환
            }

            // Memory Usage 계산
            var memoryUsage = stats.MemoryStats.Usage;
            var memoryLimit = stats.MemoryStats.Limit;
            var memoryPercent = memoryLimit > 0 ? (double)memoryUsage / memoryLimit : 0;

            // CPU Usage 계산
            var cpuDelta = stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage;
            var systemDelta = stats.CPUStats.SystemUsage - stats.PreCPUStats.SystemUsage;
            var cpuCount = stats.CPUStats.OnlineCPUs > 0 ? stats.CPUStats.OnlineCPUs : 1;
            var cpuPercent = systemDelta > 0 ? (double)cpuDelta / systemDelta * cpuCount * 100.0 : 0;

            // Disk I/O
            var diskRead = stats.BlkioStats.IoServiceBytesRecursive?
                .Where(x => x.Op == "read")
                .Sum(x => (long)x.Value) ?? 0;
            var diskWrite = stats.BlkioStats.IoServiceBytesRecursive?
                .Where(x => x.Op == "write")
                .Sum(x => (long)x.Value) ?? 0;

            // Network I/O
            var networkRx = stats.Networks?.Values.Sum(x => (long)x.RxBytes) ?? 0;
            var networkTx = stats.Networks?.Values.Sum(x => (long)x.TxBytes) ?? 0;

            // PIDs
            var pidsCount = (int)(stats.PidsStats?.Current ?? 0);

            return new ResourceUsage
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageBytes = (long)memoryUsage,
                MemoryPeakBytes = (long)(stats.MemoryStats.MaxUsage),
                MemoryUsagePercent = memoryPercent,
                CpuUsageNanoseconds = (long)stats.CPUStats.CPUUsage.TotalUsage,
                CpuUsagePercent = cpuPercent,
                CpuThrottledMicroseconds = (long)(stats.CPUStats.ThrottlingData?.ThrottledTime ?? 0) / 1000,
                DiskReadBytes = diskRead,
                DiskWriteBytes = diskWrite,
                NetworkRxBytes = networkRx,
                NetworkTxBytes = networkTx,
                ProcessCount = pidsCount
            };
        }
        catch
        {
            return new ResourceUsage(); // 빈 객체 반환
        }
    }

    /// <summary>
    /// 리소스 제한 위반 확인 (Phase 8.1)
    /// </summary>
    public async Task<ResourceViolation?> CheckViolationsAsync(
        ResourceLimits limits,
        CancellationToken cancellationToken = default)
    {
        var usage = await GetCurrentUsageAsync(cancellationToken);

        // 메모리 제한 위반 확인
        if (limits.MemoryLimitBytes.HasValue && usage.MemoryUsageBytes > 0)
        {
            if (usage.MemoryUsageBytes > limits.MemoryLimitBytes.Value)
            {
                return new ResourceViolation
                {
                    Type = ResourceViolationType.MemoryLimit,
                    CurrentValue = usage.MemoryUsageBytes,
                    LimitValue = limits.MemoryLimitBytes.Value,
                    Severity = (double)usage.MemoryUsageBytes / limits.MemoryLimitBytes.Value - 1.0,
                    Message = $"Memory usage ({usage.MemoryUsageBytes} bytes) exceeds limit ({limits.MemoryLimitBytes.Value} bytes)",
                    ShouldTerminate = true
                };
            }

            // 메모리 경고 임계값 (80%)
            if (limits.MemoryWarningBytes.HasValue && usage.MemoryUsageBytes > limits.MemoryWarningBytes.Value)
            {
                return new ResourceViolation
                {
                    Type = ResourceViolationType.MemoryWarning,
                    CurrentValue = usage.MemoryUsageBytes,
                    LimitValue = limits.MemoryWarningBytes.Value,
                    Severity = 0.5,
                    Message = $"Memory usage ({usage.MemoryUsageBytes} bytes) exceeds warning threshold ({limits.MemoryWarningBytes.Value} bytes)",
                    ShouldTerminate = false
                };
            }
        }

        // 위반 없음
        return null;
    }

    /// <summary>
    /// 리소스 사용 이력 조회 (Phase 8.1)
    /// 현재 구현에서는 이력을 저장하지 않으므로 빈 리스트 반환
    /// </summary>
    public Task<List<ResourceUsage>> GetUsageHistoryAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        // TODO: 향후 이력 저장 기능 추가 시 구현
        return Task.FromResult(new List<ResourceUsage>());
    }

    /// <summary>
    /// 리소스 사용량 조회 (IExecutionEnvironment 인터페이스 구현)
    /// GetCurrentUsageAsync()를 호출하여 결과를 nullable로 반환
    /// </summary>
    public async Task<ResourceUsage?> GetResourceUsageAsync(CancellationToken cancellationToken = default)
    {
        var usage = await GetCurrentUsageAsync(cancellationToken);

        // 빈 ResourceUsage 객체인 경우 null 반환
        if (usage.MemoryUsageBytes == 0 && usage.CpuUsagePercent == 0)
        {
            return null;
        }

        return usage;
    }

    /// <summary>
    /// ResourceLimits에서 Docker HostConfig 생성 (Phase 6.2)
    /// </summary>
    private static HostConfig BuildHostConfig(ResourceLimits? limits)
    {
        var hostConfig = new HostConfig
        {
            NetworkMode = "none",
            AutoRemove = false
        };

        if (limits == null)
        {
            // 기본 제한
            hostConfig.Memory = 512 * 1024 * 1024; // 512MB
            hostConfig.CPUShares = 1024;
            return hostConfig;
        }

        // === Memory Limits ===
        if (limits.MemoryLimitBytes.HasValue)
        {
            hostConfig.Memory = limits.MemoryLimitBytes.Value;
        }

        if (limits.MemoryReservationBytes.HasValue)
        {
            hostConfig.MemoryReservation = limits.MemoryReservationBytes.Value;
        }

        if (limits.MemorySwapLimitBytes.HasValue)
        {
            hostConfig.MemorySwap = limits.MemorySwapLimitBytes.Value;
        }

        // === CPU Limits ===
        if (limits.CpuShares.HasValue)
        {
            hostConfig.CPUShares = limits.CpuShares.Value;
        }

        if (limits.CpuQuotaMicroseconds.HasValue)
        {
            hostConfig.CPUQuota = limits.CpuQuotaMicroseconds.Value;
        }

        if (limits.CpuPeriodMicroseconds.HasValue)
        {
            hostConfig.CPUPeriod = limits.CpuPeriodMicroseconds.Value;
        }

        if (limits.CpuCount.HasValue)
        {
            // NanoCPUs = CPU count * 1e9
            hostConfig.NanoCPUs = (long)(limits.CpuCount.Value * 1_000_000_000);
        }

        // === Disk I/O Limits ===
        if (limits.DiskReadBytesPerSec.HasValue || limits.DiskWriteBytesPerSec.HasValue)
        {
            // 모든 디바이스에 적용 (major:minor = 0:0)
            var devicePath = "/dev/sda"; // 기본 디스크 (환경에 따라 다를 수 있음)

            if (limits.DiskReadBytesPerSec.HasValue)
            {
                hostConfig.BlkioDeviceReadBps = new List<ThrottleDevice>
                {
                    new ThrottleDevice
                    {
                        Path = devicePath,
                        Rate = (ulong)limits.DiskReadBytesPerSec.Value
                    }
                };
            }

            if (limits.DiskWriteBytesPerSec.HasValue)
            {
                hostConfig.BlkioDeviceWriteBps = new List<ThrottleDevice>
                {
                    new ThrottleDevice
                    {
                        Path = devicePath,
                        Rate = (ulong)limits.DiskWriteBytesPerSec.Value
                    }
                };
            }
        }

        // === Process Limits ===
        if (limits.MaxProcesses.HasValue)
        {
            hostConfig.PidsLimit = limits.MaxProcesses.Value;
        }

        // === File Descriptor Limits ===
        if (limits.MaxFileDescriptors.HasValue)
        {
            hostConfig.Ulimits = new List<Ulimit>
            {
                new Ulimit
                {
                    Name = "nofile",
                    Soft = limits.MaxFileDescriptors.Value,
                    Hard = limits.MaxFileDescriptors.Value
                }
            };
        }

        return hostConfig;
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

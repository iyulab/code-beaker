using CodeBeaker.Core.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Runtimes.Docker;

/// <summary>
/// Docker 컨테이너 정리 서비스
/// 시작 시 zombie 컨테이너 정리 및 주기적 cleanup
/// </summary>
public sealed class DockerCleanupService : IHostedService
{
    private readonly DockerClient _docker;
    private readonly ILogger<DockerCleanupService> _logger;
    private readonly TimeSpan _maxContainerAge;

    public DockerCleanupService(
        DockerClient docker,
        ILogger<DockerCleanupService> logger,
        TimeSpan? maxContainerAge = null)
    {
        _docker = docker;
        _logger = logger;
        _maxContainerAge = maxContainerAge ?? TimeSpan.FromHours(24);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Docker cleanup service starting (max container age: {MaxAge}h)",
            _maxContainerAge.TotalHours);

        try
        {
            // 시작 시 zombie 컨테이너 정리
            await CleanupZombieContainersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial zombie container cleanup");
        }

        return;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Docker cleanup service stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Zombie 컨테이너 정리
    /// codebeaker.* 라벨이 있는 오래된 컨테이너 제거
    /// </summary>
    public async Task CleanupZombieContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting zombie container cleanup");

            // CodeBeaker 컨테이너 필터링 (라벨 기반)
            var filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool>
                {
                    ["codebeaker.runtime=docker"] = true
                }
            };

            var containers = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true, // 중지된 컨테이너 포함
                    Filters = filters
                },
                cancellationToken);

            if (containers.Count == 0)
            {
                _logger.LogInformation("No CodeBeaker containers found");
                return;
            }

            _logger.LogInformation(
                "Found {Count} CodeBeaker container(s)",
                containers.Count);

            var now = DateTime.UtcNow;
            var cleanedCount = 0;

            foreach (var container in containers)
            {
                try
                {
                    // 컨테이너 생성 시각 확인
                    if (!container.Labels.TryGetValue("codebeaker.created", out var createdLabel) ||
                        string.IsNullOrEmpty(createdLabel))
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(createdLabel, out var createdAt))
                    {
                        continue;
                    }

                    var age = now - createdAt;

                    // 너무 오래된 컨테이너는 정리
                    if (age > _maxContainerAge)
                    {
                        _logger.LogWarning(
                            "Removing zombie container {ContainerId} (age: {Age:F1}h)",
                            container.ID[..12],
                            age.TotalHours);

                        // 컨테이너 중지 (이미 중지되었을 수 있음)
                        try
                        {
                            await _docker.Containers.StopContainerAsync(
                                container.ID,
                                new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                                cancellationToken);
                        }
                        catch
                        {
                            // 이미 중지된 경우 무시
                        }

                        // 컨테이너 제거
                        await _docker.Containers.RemoveContainerAsync(
                            container.ID,
                            new ContainerRemoveParameters { Force = true },
                            cancellationToken);

                        cleanedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error cleaning up container {ContainerId}",
                        container.ID[..12]);
                }
            }

            _logger.LogInformation(
                "Zombie container cleanup completed - removed {Count} container(s)",
                cleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during zombie container cleanup");
        }
    }
}

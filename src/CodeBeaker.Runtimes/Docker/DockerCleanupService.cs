using System.Globalization;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Runtimes.Docker;

/// <summary>
/// Reclaims CodeBeaker containers that outlived the session that created them.
/// It is a backstop for the ordinary cleanup paths (session close, environment
/// disposal), so it runs on a fixed interval for as long as the host lives —
/// a container leaked by an abnormal exit is collected on the next sweep, not
/// only on the next process restart.
/// </summary>
public sealed class DockerCleanupService : BackgroundService
{
    /// <summary>How often the sweep runs when no interval is supplied.</summary>
    public static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// How old a container must be before a sweep reclaims it, when no age is
    /// supplied. Deliberately far above the default session lifetime: this is a
    /// backstop, and it must never race a session that is still legitimately
    /// running under a consumer-configured longer lifetime.
    /// </summary>
    public static readonly TimeSpan DefaultMaxContainerAge = TimeSpan.FromHours(24);

    private readonly IDockerClient _docker;
    private readonly ILogger<DockerCleanupService> _logger;
    private readonly TimeSpan _maxContainerAge;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeProvider _timeProvider;

    public DockerCleanupService(
        IDockerClient docker,
        ILogger<DockerCleanupService> logger,
        TimeSpan? maxContainerAge = null,
        TimeSpan? cleanupInterval = null,
        TimeProvider? timeProvider = null)
    {
        _docker = docker;
        _logger = logger;
        _maxContainerAge = maxContainerAge ?? DefaultMaxContainerAge;
        _cleanupInterval = cleanupInterval ?? DefaultCleanupInterval;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Docker cleanup service started (interval: {Interval}, max container age: {MaxAge}h)",
            _cleanupInterval,
            _maxContainerAge.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupZombieContainersAsync(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Docker cleanup service stopped");
    }

    /// <summary>
    /// Removes CodeBeaker-labelled containers older than the configured maximum
    /// age. Safe to call directly — a sweep never throws for a container it
    /// could not reclaim; it logs and moves on to the next one.
    /// </summary>
    public async Task CleanupZombieContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
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
                    All = true, // include stopped containers
                    Filters = filters
                },
                cancellationToken);

            if (containers.Count == 0)
            {
                _logger.LogDebug("No CodeBeaker containers found");
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var cleanedCount = 0;

            foreach (var container in containers)
            {
                try
                {
                    if (!TryGetAge(container, now, out var age))
                    {
                        continue;
                    }

                    if (age <= _maxContainerAge)
                    {
                        continue;
                    }

                    _logger.LogWarning(
                        "Removing zombie container {ContainerId} (age: {Age:F1}h)",
                        container.ID[..12],
                        age.TotalHours);

                    try
                    {
                        await _docker.Containers.StopContainerAsync(
                            container.ID,
                            new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                            cancellationToken);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Already stopped, or the daemon lost it — removal below still applies.
                        _logger.LogDebug(ex, "Could not stop container {ContainerId} before removal", container.ID[..12]);
                    }

                    await _docker.Containers.RemoveContainerAsync(
                        container.ID,
                        new ContainerRemoveParameters { Force = true },
                        cancellationToken);

                    cleanedCount++;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error cleaning up container {ContainerId}", container.ID[..12]);
                }
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation(
                    "Zombie container cleanup removed {Count} container(s)",
                    cleanedCount);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error during zombie container cleanup");
        }
    }

    /// <summary>
    /// Reads the creation label as an absolute instant. The label is written as
    /// a round-trip UTC timestamp, so it must be parsed as one — parsing it into
    /// a local <see cref="DateTime"/> and subtracting it from a UTC clock skews
    /// every age by the host's own offset, in whichever direction that offset
    /// runs.
    /// </summary>
    private static bool TryGetAge(ContainerListResponse container, DateTimeOffset now, out TimeSpan age)
    {
        age = default;

        if (!container.Labels.TryGetValue("codebeaker.created", out var createdLabel) ||
            string.IsNullOrEmpty(createdLabel))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                createdLabel,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdAt))
        {
            return false;
        }

        age = now - createdAt;
        return true;
    }
}

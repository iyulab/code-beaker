using CodeBeaker.Runtimes.Docker;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CodeBeaker.Runtimes.Tests;

/// <summary>
/// The cleanup service is the backstop the other reclamation paths lean on.
/// These tests pin the two things that made it a backstop in name only: it has
/// to keep sweeping for as long as the host runs, and it has to measure a
/// container's age against the same clock the label was written with.
/// </summary>
public sealed class DockerCleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A clock frozen at <see cref="Now"/> so age assertions do not race real time.</summary>
    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ContainerListResponse Container(string id, string createdLabel) => new()
    {
        ID = id,
        Labels = new Dictionary<string, string>
        {
            ["codebeaker.runtime"] = "docker",
            ["codebeaker.created"] = createdLabel
        }
    };

    private static (Mock<IDockerClient> Docker, Mock<IContainerOperations> Containers) DockerReturning(
        params ContainerListResponse[] containers)
    {
        var operations = new Mock<IContainerOperations>();
        operations
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers.ToList());
        operations
            .Setup(c => c.StopContainerAsync(It.IsAny<string>(), It.IsAny<ContainerStopParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        operations
            .Setup(c => c.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(operations.Object);
        return (docker, operations);
    }

    private static DockerCleanupService ServiceFor(
        Mock<IDockerClient> docker,
        TimeSpan? maxAge = null,
        TimeSpan? interval = null)
        => new(
            docker.Object,
            NullLogger<DockerCleanupService>.Instance,
            maxAge ?? TimeSpan.FromHours(24),
            interval ?? TimeSpan.FromHours(1),
            new FrozenClock());

    [Fact]
    public async Task Sweep_RemovesAContainerOlderThanTheMaxAge()
    {
        var (docker, containers) = DockerReturning(
            Container("aaaaaaaaaaaa0", Now.AddHours(-25).UtcDateTime.ToString("o")));

        await ServiceFor(docker).CleanupZombieContainersAsync();

        containers.Verify(
            c => c.RemoveContainerAsync("aaaaaaaaaaaa0", It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sweep_KeepsAContainerYoungerThanTheMaxAge()
    {
        var (docker, containers) = DockerReturning(
            Container("bbbbbbbbbbbb0", Now.AddHours(-23).UtcDateTime.ToString("o")));

        await ServiceFor(docker).CleanupZombieContainersAsync();

        containers.Verify(
            c => c.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The label carries an absolute instant. Reading it into local time and
    /// subtracting from a UTC clock shifts every age by the host's own offset —
    /// under-collecting east of UTC, over-collecting west of it. The same
    /// instant written with a non-zero offset must yield the same age.
    /// </summary>
    [Fact]
    public async Task Sweep_MeasuresAgeAsAnAbsoluteInstant_NotAgainstHostLocalTime()
    {
        var twentyFiveHoursAgo = Now.AddHours(-25);
        var (docker, containers) = DockerReturning(
            Container("cccccccccccc0", twentyFiveHoursAgo.ToOffset(TimeSpan.FromHours(9)).ToString("o")));

        await ServiceFor(docker).CleanupZombieContainersAsync();

        containers.Verify(
            c => c.RemoveContainerAsync("cccccccccccc0", It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sweep_SkipsAContainerWithNoCreationLabel()
    {
        var (docker, containers) = DockerReturning(new ContainerListResponse
        {
            ID = "dddddddddddd0",
            Labels = new Dictionary<string, string> { ["codebeaker.runtime"] = "docker" }
        });

        await ServiceFor(docker).CleanupZombieContainersAsync();

        containers.Verify(
            c => c.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// One container the daemon refuses to remove must not end the sweep — the
    /// backstop exists precisely for the messy cases.
    /// </summary>
    [Fact]
    public async Task Sweep_ContinuesAfterAContainerFailsToBeRemoved()
    {
        var (docker, containers) = DockerReturning(
            Container("eeeeeeeeeeee0", Now.AddHours(-30).UtcDateTime.ToString("o")),
            Container("ffffffffffff0", Now.AddHours(-30).UtcDateTime.ToString("o")));

        containers
            .Setup(c => c.RemoveContainerAsync("eeeeeeeeeeee0", It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.Conflict, "removal in progress"));

        await ServiceFor(docker).CleanupZombieContainersAsync();

        containers.Verify(
            c => c.RemoveContainerAsync("ffffffffffff0", It.IsAny<ContainerRemoveParameters>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The defect this class was registered to prevent: a sweep that runs once
    /// at startup leaves everything the earlier paths missed in place until the
    /// process restarts.
    /// </summary>
    [Fact]
    public async Task Service_SweepsRepeatedly_NotOnlyOnceAtStartup()
    {
        var (docker, containers) = DockerReturning();
        var sweeps = 0;
        containers
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref sweeps);
                return new List<ContainerListResponse>();
            });

        using var service = ServiceFor(docker, interval: TimeSpan.FromMilliseconds(20));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.StartAsync(CancellationToken.None);
        try
        {
            while (Volatile.Read(ref sweeps) < 3 && !cts.IsCancellationRequested)
            {
                await Task.Delay(10, CancellationToken.None);
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Volatile.Read(ref sweeps).Should().BeGreaterThanOrEqualTo(3,
            "the service must keep sweeping on its interval, not stop after the startup sweep");
    }
}

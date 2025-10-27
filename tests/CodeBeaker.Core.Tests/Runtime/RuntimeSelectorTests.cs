using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Runtime;
using Moq;
using Xunit;

namespace CodeBeaker.Core.Tests.Runtime;

/// <summary>
/// RuntimeSelector 단위 테스트
/// </summary>
public sealed class RuntimeSelectorTests
{
    [Fact]
    public async Task SelectBestRuntime_ShouldReturnNull_WhenNoRuntimesAvailable()
    {
        // Arrange
        var selector = new RuntimeSelector(Array.Empty<IExecutionRuntime>());

        // Act
        var runtime = await selector.SelectBestRuntimeAsync("unknown");

        // Assert
        Assert.Null(runtime);
    }

    [Fact]
    public async Task SelectBestRuntime_ShouldReturnFastestRuntime_WhenSpeedPreferred()
    {
        // Arrange
        var slowRuntime = CreateMockRuntime("slow", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, true);
        var fastRuntime = CreateMockRuntime("fast", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, true);

        var selector = new RuntimeSelector(new[] { slowRuntime.Object, fastRuntime.Object });

        // Act
        var selected = await selector.SelectBestRuntimeAsync("deno", RuntimePreference.Speed);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal(RuntimeType.Deno, selected!.Type);
        Assert.Equal("fast", selected.Name);
    }

    [Fact]
    public async Task SelectBestRuntime_ShouldReturnMostSecureRuntime_WhenSecurityPreferred()
    {
        // Arrange
        var lessSecure = CreateMockRuntime("less", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, true);
        var moreSecure = CreateMockRuntime("more", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, true);

        var selector = new RuntimeSelector(new[] { lessSecure.Object, moreSecure.Object });

        // Act
        var selected = await selector.SelectBestRuntimeAsync("deno", RuntimePreference.Security);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal(RuntimeType.Docker, selected!.Type);
        Assert.Equal("more", selected.Name);
    }

    [Fact]
    public async Task SelectBestRuntime_ShouldReturnLowMemoryRuntime_WhenMemoryPreferred()
    {
        // Arrange
        var highMemory = CreateMockRuntime("high", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, true);
        var lowMemory = CreateMockRuntime("low", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, true);

        var selector = new RuntimeSelector(new[] { highMemory.Object, lowMemory.Object });

        // Act
        var selected = await selector.SelectBestRuntimeAsync("deno", RuntimePreference.Memory);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal(RuntimeType.Deno, selected!.Type);
        Assert.Equal("low", selected.Name);
    }

    [Fact]
    public async Task SelectBestRuntime_ShouldFilterUnavailableRuntimes()
    {
        // Arrange
        var unavailable = CreateMockRuntime("unavailable", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, false);
        var available = CreateMockRuntime("available", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, true);

        var selector = new RuntimeSelector(new[] { unavailable.Object, available.Object });

        // Act
        var selected = await selector.SelectBestRuntimeAsync("deno");

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("available", selected!.Name);
    }

    [Fact]
    public async Task SelectByTypeAsync_ShouldReturnSpecificRuntimeType()
    {
        // Arrange
        var dockerRuntime = CreateMockRuntime("docker", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, true);
        var denoRuntime = CreateMockRuntime("deno", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, true);

        var selector = new RuntimeSelector(new[] { dockerRuntime.Object, denoRuntime.Object });

        // Act
        var selected = await selector.SelectByTypeAsync(RuntimeType.Deno, "deno");

        // Assert
        Assert.NotNull(selected);
        Assert.Equal(RuntimeType.Deno, selected!.Type);
        Assert.Equal("deno", selected.Name);
    }

    [Fact]
    public async Task GetAvailableRuntimesAsync_ShouldReturnOnlyAvailableRuntimes()
    {
        // Arrange
        var available1 = CreateMockRuntime("available1", RuntimeType.Docker, new[] { "deno" }, 2000, 300, 9, true);
        var unavailable = CreateMockRuntime("unavailable", RuntimeType.Deno, new[] { "deno" }, 80, 30, 7, false);
        var available2 = CreateMockRuntime("available2", RuntimeType.Bun, new[] { "deno" }, 50, 25, 6, true);

        var selector = new RuntimeSelector(new[] { available1.Object, unavailable.Object, available2.Object });

        // Act
        var runtimes = await selector.GetAvailableRuntimesAsync("deno");

        // Assert
        Assert.Equal(2, runtimes.Count);
        Assert.DoesNotContain(runtimes, r => r.Name == "unavailable");
    }

    [Fact]
    public void Constructor_ShouldGroupRuntimesByEnvironment()
    {
        // Arrange
        var pythonRuntime = CreateMockRuntime("python", RuntimeType.Docker, new[] { "python" }, 2000, 250, 9, true);
        var denoRuntime = CreateMockRuntime("deno", RuntimeType.Deno, new[] { "deno", "typescript", "javascript" }, 80, 30, 7, true);

        // Act
        var selector = new RuntimeSelector(new[] { pythonRuntime.Object, denoRuntime.Object });

        // Assert - No exception thrown, mapping should work correctly
        Assert.NotNull(selector);
    }

    private Mock<IExecutionRuntime> CreateMockRuntime(
        string name,
        RuntimeType type,
        string[] environments,
        int startupMs,
        int memoryMB,
        int isolationLevel,
        bool isAvailable)
    {
        var mock = new Mock<IExecutionRuntime>();
        mock.Setup(r => r.Name).Returns(name);
        mock.Setup(r => r.Type).Returns(type);
        mock.Setup(r => r.SupportedEnvironments).Returns(environments);
        mock.Setup(r => r.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isAvailable);
        mock.Setup(r => r.GetCapabilities()).Returns(new RuntimeCapabilities
        {
            StartupTimeMs = startupMs,
            MemoryOverheadMB = memoryMB,
            IsolationLevel = isolationLevel,
            SupportsFilesystemPersistence = true,
            SupportsNetworkAccess = true,
            MaxConcurrentExecutions = 100
        });

        return mock;
    }
}

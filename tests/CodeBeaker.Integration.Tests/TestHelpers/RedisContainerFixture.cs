using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using StackExchange.Redis;
using Xunit;

namespace CodeBeaker.Integration.Tests.TestHelpers;

/// <summary>
/// 테스트가 쓸 Redis 서버를 컨테이너로 띄운다. 데몬을 직접 몰아 붙이는 이유는
/// 이 리포가 이미 <c>Docker.DotNet</c>으로 컨테이너 수명주기를 다루기 때문이다 —
/// 같은 일을 위해 별도 테스트 컨테이너 라이브러리를 들이지 않는다.
///
/// Docker 를 못 쓰면 <see cref="SkipReason"/> 에 이유가 담기고 각 테스트가 스스로 스킵한다.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private const string Image = "redis:7-alpine";
    private const string ContainerPort = "6379/tcp";

    private readonly DockerClient _docker = new DockerClientBuilder().Build();
    private string? _containerId;

    /// <summary>
    /// 준비에 실패한 이유. <see langword="null"/> 이면 <see cref="Connection"/> 을 쓸 수 있다.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// 살아있는 Redis 연결. <see cref="SkipReason"/> 이 <see langword="null"/> 일 때만 유효하다.
    /// </summary>
    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await _docker.System.PingAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Docker is not running ({ex.GetType().Name}). Start Docker Desktop and try again.";
            return;
        }

        try
        {
            await EnsureImageAsync();
            var port = await StartContainerAsync();
            Connection = await ConnectAsync(port);
        }
        catch (Exception ex)
        {
            SkipReason = $"Could not start a Redis container: {ex.Message}";
            await DisposeAsync();
        }
    }

    private async Task EnsureImageAsync()
    {
        var existing = await _docker.Images.ListImagesAsync(new ImagesListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["reference"] = new Dictionary<string, bool> { [Image] = true }
            }
        });

        if (existing.Any())
        {
            return;
        }

        var separator = Image.LastIndexOf(':');
        await _docker.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = Image[..separator],
                Tag = Image[(separator + 1)..]
            },
            authConfig: null,
            new Progress<JSONMessage>());
    }

    private async Task<int> StartContainerAsync()
    {
        var created = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = Image,
            // 호스트 포트를 비워 두면 데몬이 빈 포트를 골라 준다 — 고정 포트를 쓰면
            // 개발 머신에서 이미 돌고 있는 Redis 와 충돌한다.
            ExposedPorts = new Dictionary<string, EmptyStruct> { [ContainerPort] = default },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [ContainerPort] = [new PortBinding { HostPort = string.Empty }]
                },
                AutoRemove = true
            },
            Labels = new Dictionary<string, string> { ["codebeaker.test"] = "redis" }
        });

        _containerId = created.ID;
        await _docker.Containers.StartContainerAsync(_containerId, new ContainerStartParameters());

        var inspect = await _docker.Containers.InspectContainerAsync(_containerId);
        var binding = inspect.NetworkSettings?.Ports?[ContainerPort]?.FirstOrDefault()
            ?? throw new InvalidOperationException("the daemon published no host port for Redis");

        return int.Parse(binding.HostPort);
    }

    private static async Task<IConnectionMultiplexer> ConnectAsync(int port)
    {
        // 컨테이너가 시작됐다고 Redis 가 접속을 받는 것은 아니다. 준비될 때까지만 재시도한다.
        var options = ConfigurationOptions.Parse($"localhost:{port}");
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 2000;

        var stopwatch = Stopwatch.StartNew();
        Exception? last = null;

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                var connection = await ConnectionMultiplexer.ConnectAsync(options);
                if (connection.IsConnected)
                {
                    await connection.GetDatabase().PingAsync();
                    return connection;
                }

                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Redis did not accept connections within 30s on port {port}.", last);
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
        {
            await Connection.DisposeAsync();
            Connection = null!;
        }

        if (_containerId is not null)
        {
            try
            {
                // AutoRemove 가 걸려 있어 정지하면 데몬이 걷어 간다.
                await _docker.Containers.StopContainerAsync(
                    _containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 1 });
            }
            catch (DockerApiException)
            {
                // 이미 사라졌다면 할 일이 없다.
            }

            _containerId = null;
        }

        _docker.Dispose();
    }
}

using Docker.DotNet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeBeaker.Runtimes.Docker;

/// <summary>
/// Docker daemon 연결 모니터
/// Docker daemon 재시작 시 자동 재연결
/// </summary>
public sealed class DockerConnectionMonitor : BackgroundService
{
    private readonly DockerClient _docker;
    private readonly ILogger<DockerConnectionMonitor> _logger;
    private readonly TimeSpan _checkInterval;
    private bool _isConnected = true;

    public DockerConnectionMonitor(
        DockerClient docker,
        ILogger<DockerConnectionMonitor> logger,
        TimeSpan? checkInterval = null)
    {
        _docker = docker;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Docker connection monitor started (check interval: {Interval}s)",
            _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckConnectionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Docker connection check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Docker connection monitor stopped");
    }

    private async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Docker daemon ping
            await _docker.System.PingAsync(cancellationToken);

            // 재연결 성공
            if (!_isConnected)
            {
                _logger.LogInformation("Docker daemon reconnected successfully");
                _isConnected = true;
            }
        }
        catch (Exception ex)
        {
            // 연결 실패
            if (_isConnected)
            {
                _logger.LogWarning(
                    ex,
                    "Lost connection to Docker daemon - will retry");
                _isConnected = false;
            }
        }
    }

    /// <summary>
    /// Docker 연결 상태 확인
    /// </summary>
    public bool IsConnected => _isConnected;
}

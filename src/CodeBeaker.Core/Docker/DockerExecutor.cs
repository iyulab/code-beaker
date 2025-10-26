using Docker.DotNet;
using Docker.DotNet.Models;
using CodeBeaker.Core.Models;
using System.Diagnostics;
using System.Text;

namespace CodeBeaker.Core.Docker;

/// <summary>
/// Docker 컨테이너 실행기
/// </summary>
public sealed class DockerExecutor
{
    private readonly DockerClient _client;

    public DockerExecutor(string dockerHost = "unix:///var/run/docker.sock")
    {
        // Windows에서는 npipe 사용
        if (OperatingSystem.IsWindows())
        {
            dockerHost = "npipe://./pipe/docker_engine";
        }

        _client = new DockerClientConfiguration(new Uri(dockerHost))
            .CreateClient();
    }

    /// <summary>
    /// Docker 컨테이너에서 코드 실행
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        string image,
        string[] command,
        string workspaceDir,
        ExecutionConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var executionId = Guid.NewGuid().ToString();

        try
        {
            // Create container
            var container = await CreateContainerAsync(image, command, workspaceDir, config, cancellationToken);

            try
            {
                // Start container
                await _client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters(), cancellationToken);

                // Wait for container with timeout
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.Timeout));

                ContainerWaitResponse? waitResponse = null;
                var timedOut = false;

                try
                {
                    waitResponse = await _client.Containers.WaitContainerAsync(container.ID, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred
                    timedOut = true;

                    // Kill the container
                    try
                    {
                        await _client.Containers.KillContainerAsync(container.ID, new ContainerKillParameters(), cancellationToken);
                    }
                    catch
                    {
                        // Ignore kill errors
                    }
                }

                // Get logs
                var (stdout, stderr) = await GetLogsAsync(container.ID, cancellationToken);

                sw.Stop();

                if (timedOut)
                {
                    return new ExecutionResult
                    {
                        ExecutionId = executionId,
                        Status = "failed",
                        ExitCode = 124, // Timeout exit code
                        Stdout = stdout,
                        Stderr = stderr,
                        DurationMs = sw.ElapsedMilliseconds,
                        Timeout = true,
                        ErrorType = "timeout_error",
                        CompletedAt = DateTime.UtcNow
                    };
                }

                var exitCode = (int)(waitResponse?.StatusCode ?? 0);

                return new ExecutionResult
                {
                    ExecutionId = executionId,
                    Status = exitCode == 0 ? "completed" : "failed",
                    ExitCode = exitCode,
                    Stdout = stdout,
                    Stderr = stderr,
                    DurationMs = sw.ElapsedMilliseconds,
                    Timeout = false,
                    ErrorType = exitCode != 0 ? DetermineErrorType(stderr) : null,
                    CompletedAt = DateTime.UtcNow
                };
            }
            finally
            {
                // Cleanup container
                try
                {
                    await _client.Containers.RemoveContainerAsync(
                        container.ID,
                        new ContainerRemoveParameters { Force = true },
                        cancellationToken);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult
            {
                ExecutionId = executionId,
                Status = "failed",
                ExitCode = -1,
                Stdout = string.Empty,
                Stderr = $"Docker execution error: {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
                Timeout = false,
                ErrorType = "docker_error",
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<CreateContainerResponse> CreateContainerAsync(
        string image,
        string[] command,
        string workspaceDir,
        ExecutionConfig config,
        CancellationToken cancellationToken)
    {
        var hostConfig = new HostConfig
        {
            // Resource limits
            Memory = config.MemoryLimit * 1024L * 1024L, // Convert MB to bytes
            NanoCPUs = (long)(config.CpuLimit * 1_000_000_000L),

            // Security
            ReadonlyRootfs = config.ReadOnlyFilesystem,
            NetworkMode = config.DisableNetwork ? "none" : "bridge",

            // Workspace mount
            Binds = new List<string>
            {
                $"{Path.GetFullPath(workspaceDir)}:/workspace:rw"
            },

            // Tmpfs for writable temp directory
            Tmpfs = new Dictionary<string, string>
            {
                { "/tmp", "rw,noexec,nosuid,size=100m" }
            }
        };

        var containerConfig = new CreateContainerParameters
        {
            Image = image,
            Cmd = command,
            WorkingDir = "/workspace",
            HostConfig = hostConfig,
            Env = config.Environment.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            AttachStdout = true,
            AttachStderr = true,
            Tty = false
        };

        return await _client.Containers.CreateContainerAsync(containerConfig, cancellationToken);
    }

    private async Task<(string stdout, string stderr)> GetLogsAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        var parameters = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = false
        };

        var stream = await _client.Containers.GetContainerLogsAsync(
            containerId,
            false, // tty=false for proper demultiplexing
            parameters,
            cancellationToken);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        // MultiplexedStream provides automatic demultiplexing
        using var stdoutStream = new MemoryStream();
        using var stderrStream = new MemoryStream();
        using var stdinStream = Stream.Null; // No stdin needed

        await stream.CopyOutputToAsync(stdinStream, stdoutStream, stderrStream, cancellationToken);

        stdoutStream.Position = 0;
        stderrStream.Position = 0;

        using var stdoutReader = new StreamReader(stdoutStream);
        using var stderrReader = new StreamReader(stderrStream);

        var stdoutText = await stdoutReader.ReadToEndAsync();
        var stderrText = await stderrReader.ReadToEndAsync();

        return (stdoutText, stderrText);
    }

    private static string? DetermineErrorType(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "runtime_error";
        }

        var lowerStderr = stderr.ToLowerInvariant();

        if (lowerStderr.Contains("syntaxerror") || lowerStderr.Contains("syntax error"))
        {
            return "syntax_error";
        }

        if (lowerStderr.Contains("timeout") || lowerStderr.Contains("timed out"))
        {
            return "timeout_error";
        }

        if (lowerStderr.Contains("memory") || lowerStderr.Contains("out of memory"))
        {
            return "memory_error";
        }

        return "runtime_error";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

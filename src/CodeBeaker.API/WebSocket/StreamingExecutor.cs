using CodeBeaker.Core.Docker;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.API.WebSocket;

/// <summary>
/// Real-time streaming executor for code execution
/// </summary>
public sealed class StreamingExecutor
{
    private readonly ILogger<StreamingExecutor> _logger;

    public StreamingExecutor(ILogger<StreamingExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute code with real-time stdout/stderr streaming
    /// </summary>
    public async Task<ExecutionResult> ExecuteWithStreamingAsync(
        IRuntime runtime,
        string code,
        ExecutionConfig config,
        IJsonRpcTransport transport,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();

        try
        {
            // Send execution started notification
            await transport.SendNotificationAsync("execution.started", new
            {
                executionId,
                language = runtime.LanguageName
            }, cancellationToken);

            // Execute code (currently non-streaming, will be enhanced in next step)
            var result = await runtime.ExecuteAsync(code, config, cancellationToken);

            // Send output as notifications (simulating streaming)
            if (!string.IsNullOrEmpty(result.Stdout))
            {
                await transport.SendNotificationAsync("execution.output", new
                {
                    executionId,
                    stream = "stdout",
                    text = result.Stdout
                }, cancellationToken);
            }

            if (!string.IsNullOrEmpty(result.Stderr))
            {
                await transport.SendNotificationAsync("execution.output", new
                {
                    executionId,
                    stream = "stderr",
                    text = result.Stderr
                }, cancellationToken);
            }

            // Send execution completed notification
            await transport.SendNotificationAsync("execution.completed", new
            {
                executionId,
                exitCode = result.ExitCode,
                durationMs = result.DurationMs,
                timeout = result.Timeout
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming execution failed: {ExecutionId}", executionId);

            // Send error notification
            await transport.SendNotificationAsync("execution.error", new
            {
                executionId,
                error = ex.Message
            }, cancellationToken);

            throw;
        }
    }
}

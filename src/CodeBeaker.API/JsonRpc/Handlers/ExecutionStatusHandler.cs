using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.JsonRpc.Interfaces;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "execution.status" JSON-RPC method
/// </summary>
public sealed class ExecutionStatusHandler : IJsonRpcHandler
{
    private readonly IStorage _storage;
    private readonly ILogger<ExecutionStatusHandler> _logger;

    public string Method => "execution.status";

    public ExecutionStatusHandler(
        IStorage storage,
        ILogger<ExecutionStatusHandler> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default)
    {
        // Parse parameters
        var json = JsonSerializer.Serialize(@params);
        var request = JsonSerializer.Deserialize<ExecutionStatusRequest>(json);

        if (request == null || string.IsNullOrWhiteSpace(request.ExecutionId))
        {
            throw new ArgumentException("Invalid parameters: executionId is required");
        }

        // Get result from storage
        var result = await _storage.GetResultAsync(request.ExecutionId, cancellationToken);

        if (result == null)
        {
            return new
            {
                executionId = request.ExecutionId,
                status = "not_found"
            };
        }

        return new
        {
            executionId = request.ExecutionId,
            status = result.ExitCode == 0 ? "completed" : "failed",
            exitCode = result.ExitCode,
            stdout = result.Stdout,
            stderr = result.Stderr,
            durationMs = result.DurationMs,
            timeout = result.Timeout
        };
    }

    private sealed class ExecutionStatusRequest
    {
        public string ExecutionId { get; set; } = string.Empty;
    }
}

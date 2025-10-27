using System.Text.Json;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.Runtimes;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "execution.run" JSON-RPC method
/// </summary>
public sealed class ExecutionRunHandler : IJsonRpcHandler
{
    private readonly IQueue _queue;
    private readonly IStorage _storage;
    private readonly ILogger<ExecutionRunHandler> _logger;

    public string Method => "execution.run";

    public ExecutionRunHandler(
        IQueue queue,
        IStorage storage,
        ILogger<ExecutionRunHandler> logger)
    {
        _queue = queue;
        _storage = storage;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default)
    {
        // Parse parameters
        var json = JsonSerializer.Serialize(@params);
        var request = JsonSerializer.Deserialize<ExecutionRunRequest>(json);

        if (request == null || string.IsNullOrWhiteSpace(request.Language) || string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("Invalid parameters: language and code are required");
        }

        // Submit to queue
        var config = new ExecutionConfig
        {
            Timeout = request.Timeout ?? 30,
            MemoryLimit = request.MemoryLimit ?? 512,
            CpuLimit = request.CpuLimit ?? 1.0
        };

        var executionId = await _queue.SubmitTaskAsync(
            request.Code,
            request.Language,
            config,
            cancellationToken
        );

        _logger.LogInformation("Execution submitted via JSON-RPC: {ExecutionId}", executionId);

        // Return execution ID
        return new
        {
            executionId,
            status = "queued"
        };
    }

    private sealed class ExecutionRunRequest
    {
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<string>? Packages { get; set; }
        public int? Timeout { get; set; }
        public int? MemoryLimit { get; set; }
        public double? CpuLimit { get; set; }
        public bool? NetworkEnabled { get; set; }
    }
}

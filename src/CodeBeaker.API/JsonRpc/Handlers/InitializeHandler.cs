using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.Runtimes;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "initialize" JSON-RPC method (capabilities negotiation)
/// </summary>
public sealed class InitializeHandler : IJsonRpcHandler
{
    public string Method => "initialize";

    public Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default)
    {
        // Return server capabilities
        var capabilities = new
        {
            serverCapabilities = new
            {
                supportsStreaming = true,
                supportsDebugging = false, // Future feature
                supportsPortForwarding = false, // Future feature
                supportsFileWatch = false, // Future feature
                supportedLanguages = RuntimeRegistry.GetSupportedLanguages(),
                limits = new
                {
                    maxTimeout = 300,
                    maxMemory = 2048,
                    maxConcurrency = 10
                },
                protocolVersion = "0.2.0"
            }
        };

        return Task.FromResult<object?>(capabilities);
    }
}

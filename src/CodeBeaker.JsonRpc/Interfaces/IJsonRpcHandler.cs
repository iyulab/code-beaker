namespace CodeBeaker.JsonRpc.Interfaces;

/// <summary>
/// JSON-RPC method handler interface
/// </summary>
public interface IJsonRpcHandler
{
    /// <summary>
    /// Get method name this handler is responsible for
    /// </summary>
    string Method { get; }

    /// <summary>
    /// Execute the method with given parameters
    /// </summary>
    /// <param name="params">Method parameters (object or array)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result object</returns>
    Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default);
}

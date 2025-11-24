using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.Runtimes;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "language.list" JSON-RPC method
/// </summary>
public sealed class LanguageListHandler : IJsonRpcHandler
{
    public string Method => "language.list";

    public Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default)
    {
        var languages = RuntimeRegistry.GetSupportedLanguages();

        var result = new
        {
            languages = languages.Select(lang => new
            {
                name = lang,
                runtime = RuntimeRegistry.Get(lang).GetType().Name
            }).ToList()
        };

        return Task.FromResult<object?>(result);
    }
}

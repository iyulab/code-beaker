using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.Runtimes;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "language.list" JSON-RPC method — the same catalog the REST
/// <c>/api/language</c> endpoint serves, in the same shape.
/// </summary>
public sealed class LanguageListHandler : IJsonRpcHandler
{
    public string Method => "language.list";

    public Task<object?> HandleAsync(object? @params, CancellationToken cancellationToken = default)
    {
        var result = new
        {
            languages = LanguageCatalog.All.Select(language => new
            {
                name = language.Name,
                displayName = language.DisplayName,
                version = language.Version,
                aliases = language.Aliases,
                dockerImage = language.DockerImage
            }).ToList()
        };

        return Task.FromResult<object?>(result);
    }
}

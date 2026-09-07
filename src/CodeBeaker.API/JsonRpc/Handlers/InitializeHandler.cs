using System.Reflection;
using CodeBeaker.JsonRpc.Interfaces;
using CodeBeaker.Runtimes;

namespace CodeBeaker.API.JsonRpc.Handlers;

/// <summary>
/// Handler for "initialize" JSON-RPC method (capabilities negotiation)
/// </summary>
public sealed class InitializeHandler : IJsonRpcHandler
{
    /// <summary>
    /// 어셈블리 버전에서 도출한다 — 이 값은 릴리스마다 바뀌는데 상수로 두면 갱신을 잊는다.
    /// 실제로 초기 커밋 이후 한 번도 갱신되지 않아, 와이어 형태가 두 차례 바뀌는 동안
    /// 계속 같은 값을 광고하고 있었다. 버전의 단일 출처는 <c>Directory.Build.props</c> 이고
    /// 세 패키지와 API 서버가 같은 버전으로 함께 나간다.
    /// </summary>
    private static string ProtocolVersion { get; } = ResolveProtocolVersion();

    private static string ResolveProtocolVersion()
    {
        var version = typeof(InitializeHandler).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

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
                supportedLanguages = LanguageCatalog.Names,
                limits = new
                {
                    maxTimeout = 300,
                    maxMemory = 2048,
                    maxConcurrency = 10
                },
                protocolVersion = ProtocolVersion
            }
        };

        return Task.FromResult<object?>(capabilities);
    }
}

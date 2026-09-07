using System.Text.Json;
using CodeBeaker.API.JsonRpc.Handlers;
using CodeBeaker.Runtimes;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// <c>initialize</c> 는 클라이언트가 서버 능력을 협상하는 첫 호출이고, 그 응답의
/// <c>protocolVersion</c> 은 클라이언트가 분기 근거로 쓰는 값이다. 상수로 두면
/// 갱신을 잊는다 — 실제로 초기 커밋 이후 한 번도 갱신되지 않은 채 와이어 형태가
/// 두 차례 바뀌었다. 이 테스트는 그 값이 빌드된 버전을 따라가는지 고정한다.
/// </summary>
public sealed class InitializeHandlerTests
{
    private static async Task<JsonElement> InitializeAsync()
    {
        var response = await new InitializeHandler().HandleAsync(null);
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(response))
            .GetProperty("serverCapabilities");
    }

    [Fact]
    public async Task Initialize_ReportsTheVersionThisServerWasBuiltAs()
    {
        var assemblyVersion = typeof(InitializeHandler).Assembly.GetName().Version;
        Assert.NotNull(assemblyVersion);
        var expected = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

        var capabilities = await InitializeAsync();

        Assert.Equal(expected, capabilities.GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task Initialize_AdvertisesTheLanguageCatalog()
    {
        var capabilities = await InitializeAsync();

        var advertised = capabilities.GetProperty("supportedLanguages")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        Assert.Equal(LanguageCatalog.Names, advertised);
    }
}

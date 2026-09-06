using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// 생성된 OpenAPI 문서 자체를 검사한다.
///
/// 이 문서는 공개 API의 얼굴인데 그동안 테스트가 한 건도 없었다 — 스위트가 전부
/// 초록인 상태에서 문서의 연락처 URL 이 템플릿 플레이스홀더(`yourusername`)를
/// 가리키고 있었고, 서버를 직접 띄워 문서를 받아보고서야 드러났다.
/// 그 부류를 사람이 아니라 검사가 잡게 한다.
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<OpenApiDocumentTests.DevelopmentApiFixture>
{
    /// <summary>
    /// Swagger 미들웨어는 Development 환경에서만 배선되므로 기본 픽스처로는 문서에 닿지 못한다.
    /// </summary>
    public sealed class DevelopmentApiFixture : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Development");
    }

    private readonly HttpClient _client;

    public OpenApiDocumentTests(DevelopmentApiFixture fixture) => _client = fixture.CreateClient();

    private async Task<JsonDocument> GetDocumentAsync()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Document_IsServedAndCarriesItsIdentity()
    {
        using var doc = await GetDocumentAsync();
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("openapi").GetString()));

        var info = root.GetProperty("info");
        Assert.Equal("CodeBeaker API", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("description").GetString()));
    }

    /// <summary>
    /// 문서에 실리는 모든 URL 은 실제로 존재하는 곳을 가리켜야 한다. 템플릿에서 흔히
    /// 남는 자리표시자는 문법적으로 멀쩡한 URL 이라 형식 검사로는 걸리지 않는다.
    /// </summary>
    [Fact]
    public async Task Document_CarriesNoPlaceholderUrls()
    {
        using var doc = await GetDocumentAsync();

        var placeholders = new[]
        {
            "yourusername", "your-username", "your_org", "example.com",
            "changeme", "TODO", "FIXME"
        };

        var raw = doc.RootElement.GetRawText();
        foreach (var placeholder in placeholders)
        {
            Assert.DoesNotContain(placeholder, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Document_ContactAndLicenseAreAbsoluteUrls()
    {
        using var doc = await GetDocumentAsync();
        var info = doc.RootElement.GetProperty("info");

        foreach (var section in new[] { "contact", "license" })
        {
            var url = info.GetProperty(section).GetProperty("url").GetString();
            Assert.False(string.IsNullOrWhiteSpace(url));
            Assert.True(
                Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                parsed.Scheme == Uri.UriSchemeHttps,
                $"info.{section}.url is not an absolute https URL: {url}");
        }
    }
}

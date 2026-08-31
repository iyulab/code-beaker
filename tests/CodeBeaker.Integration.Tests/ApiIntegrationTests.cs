using System.Net;
using System.Net.Http.Json;
using CodeBeaker.Integration.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// API 통합 테스트
/// WebApplicationFactory를 사용하여 in-memory API 서버 테스트
/// </summary>
public class ApiIntegrationTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    private readonly ApiTestFixture _fixture;

    public ApiIntegrationTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        // New health check returns JSON with detailed status
        content.Should().Contain("\"status\"");
        content.Should().Contain("healthy");
    }

    [Fact]
    public async Task GetLanguages_ShouldReturnAllSupportedLanguages()
    {
        // Act
        var response = await _client.GetAsync("/api/language");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var languages = await response.Content.ReadFromJsonAsync<List<LanguageInfo>>();
        languages.Should().NotBeNull();
        languages.Should().HaveCount(4);
        languages.Should().Contain(l => l.Name == "python");
        languages.Should().Contain(l => l.Name == "javascript");
        languages.Should().Contain(l => l.Name == "go");
        languages.Should().Contain(l => l.Name == "csharp");
    }

    [Fact]
    public async Task GetLanguage_WithValidName_ShouldReturnLanguage()
    {
        // Act
        var response = await _client.GetAsync("/api/language/python");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var language = await response.Content.ReadFromJsonAsync<LanguageInfo>();
        language.Should().NotBeNull();
        language!.Name.Should().Be("python");
        language.DisplayName.Should().Be("Python");
    }

    [Fact]
    public async Task GetLanguage_WithInvalidName_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/language/invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // DTO classes for deserialization
    private record LanguageInfo(
        string Name,
        string DisplayName,
        string Version,
        List<string> Aliases,
        string DockerImage
    );
}

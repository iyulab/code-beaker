using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        content.Should().Be("Healthy");
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

    [Fact]
    public async Task ExecuteCode_WithValidRequest_ShouldReturnExecutionId()
    {
        // Arrange
        var request = new
        {
            code = "print('Hello World')",
            language = "python"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/execution", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.ExecutionId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be("pending");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteCode_WithUnsupportedLanguage_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        var request = new
        {
            code = "console.log('test')",
            language = "ruby"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/execution", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ExecuteCode_WithEmptyCode_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            code = "",
            language = "python"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/execution", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetExecutionStatus_WithValidId_ShouldReturnStatus()
    {
        // Arrange - 먼저 실행 생성
        var request = new
        {
            code = "print('test')",
            language = "python"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/execution", request);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ExecuteResponse>();

        // Act
        var response = await _client.GetAsync($"/api/execution/{createResult!.ExecutionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<StatusResponse>();
        status.Should().NotBeNull();
        status!.ExecutionId.Should().Be(createResult.ExecutionId);
        status.Status.Should().BeOneOf("pending", "running", "completed");
    }

    [Fact]
    public async Task GetExecutionStatus_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/execution/invalid-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteCode_WithConfig_ShouldAcceptConfiguration()
    {
        // Arrange
        var request = new
        {
            code = "print('test')",
            language = "python",
            config = new
            {
                timeout = 10,
                memoryLimit = 512,
                cpuLimit = 1.0
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/execution", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // DTO classes for deserialization
    private record LanguageInfo(
        string Name,
        string DisplayName,
        string Version,
        List<string> Aliases,
        string DockerImage
    );

    private record ExecuteResponse(
        string ExecutionId,
        string Status,
        DateTime CreatedAt
    );

    private record StatusResponse(
        string ExecutionId,
        string Status,
        int? ExitCode,
        string? Stdout,
        string? Stderr,
        long? DurationMs,
        bool? Timeout,
        string? ErrorType,
        DateTime CreatedAt,
        DateTime? CompletedAt
    );
}

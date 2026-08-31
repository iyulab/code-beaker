using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CodeBeaker.Integration.Tests.TestHelpers;

/// <summary>
/// API 통합 테스트를 위한 Fixture
/// WebApplicationFactory를 사용하여 in-memory API 서버 생성
/// </summary>
public class ApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}

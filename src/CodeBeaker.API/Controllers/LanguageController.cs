using CodeBeaker.API.Models;
using CodeBeaker.Runtimes;
using Microsoft.AspNetCore.Mvc;

namespace CodeBeaker.API.Controllers;

/// <summary>
/// 지원 언어 정보 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LanguageController : ControllerBase
{
    private readonly ILogger<LanguageController> _logger;

    public LanguageController(ILogger<LanguageController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 지원하는 모든 언어 목록 조회
    /// </summary>
    /// <returns>지원 언어 목록</returns>
    /// <response code="200">언어 목록 조회 성공</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<LanguageInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<LanguageInfo>> GetLanguages()
    {
        var languages = new List<LanguageInfo>
        {
            new()
            {
                Name = "python",
                DisplayName = "Python",
                Version = "3.12",
                Aliases = new List<string> { "python", "py" },
                DockerImage = "codebeaker-python:latest"
            },
            new()
            {
                Name = "javascript",
                DisplayName = "JavaScript (Node.js)",
                Version = "20",
                Aliases = new List<string> { "javascript", "js", "node" },
                DockerImage = "codebeaker-nodejs:latest"
            },
            new()
            {
                Name = "go",
                DisplayName = "Go",
                Version = "1.21",
                Aliases = new List<string> { "go", "golang" },
                DockerImage = "codebeaker-golang:latest"
            },
            new()
            {
                Name = "csharp",
                DisplayName = "C# (.NET)",
                Version = "8.0",
                Aliases = new List<string> { "csharp", "cs", "dotnet" },
                DockerImage = "codebeaker-dotnet:latest"
            }
        };

        _logger.LogInformation("Languages list requested");

        return Ok(languages);
    }

    /// <summary>
    /// 특정 언어 지원 여부 확인
    /// </summary>
    /// <param name="name">언어 이름</param>
    /// <returns>지원 여부</returns>
    /// <response code="200">언어 지원함</response>
    /// <response code="404">언어 지원하지 않음</response>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(LanguageInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<LanguageInfo> GetLanguage(string name)
    {
        if (!RuntimeRegistry.IsSupported(name))
        {
            _logger.LogWarning("Language not supported: {Language}", name);
            return NotFound(new ErrorResponse
            {
                Code = "LANGUAGE_NOT_FOUND",
                Message = $"Language '{name}' is not supported"
            });
        }

        // 정규화된 언어 이름으로 런타임 가져오기
        var runtime = RuntimeRegistry.Get(name);
        var languageName = runtime.LanguageName;

        // 언어 정보 매핑
        var languageMap = new Dictionary<string, LanguageInfo>
        {
            ["python"] = new()
            {
                Name = "python",
                DisplayName = "Python",
                Version = "3.12",
                Aliases = new List<string> { "python", "py" },
                DockerImage = "codebeaker-python:latest"
            },
            ["javascript"] = new()
            {
                Name = "javascript",
                DisplayName = "JavaScript (Node.js)",
                Version = "20",
                Aliases = new List<string> { "javascript", "js", "node" },
                DockerImage = "codebeaker-nodejs:latest"
            },
            ["go"] = new()
            {
                Name = "go",
                DisplayName = "Go",
                Version = "1.21",
                Aliases = new List<string> { "go", "golang" },
                DockerImage = "codebeaker-golang:latest"
            },
            ["csharp"] = new()
            {
                Name = "csharp",
                DisplayName = "C# (.NET)",
                Version = "8.0",
                Aliases = new List<string> { "csharp", "cs", "dotnet" },
                DockerImage = "codebeaker-dotnet:latest"
            }
        };

        _logger.LogInformation("Language info requested: {Language}", name);

        return Ok(languageMap[languageName]);
    }
}

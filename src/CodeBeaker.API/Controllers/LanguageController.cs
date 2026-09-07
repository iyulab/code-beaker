using CodeBeaker.API.Models;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes;
using Microsoft.AspNetCore.Mvc;

namespace CodeBeaker.API.Controllers;

/// <summary>
/// 지원 언어 정보 API — <see cref="LanguageCatalog"/>를 그대로 노출한다.
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
        _logger.LogInformation("Languages list requested");

        return Ok(LanguageCatalog.All.Select(ToInfo).ToList());
    }

    /// <summary>
    /// 특정 언어 지원 여부 확인. 이름과 별칭 모두 받는다.
    /// </summary>
    /// <param name="name">언어 이름 또는 별칭</param>
    /// <returns>언어 정보</returns>
    /// <response code="200">언어 지원함</response>
    /// <response code="404">언어 지원하지 않음</response>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(LanguageInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<LanguageInfo> GetLanguage(string name)
    {
        if (!LanguageCatalog.TryResolve(name, out var language))
        {
            _logger.LogWarning("Language not supported: {Language}", name);
            return NotFound(new ErrorResponse
            {
                Code = "LANGUAGE_NOT_FOUND",
                Message = $"Language '{name}' is not supported"
            });
        }

        _logger.LogInformation("Language info requested: {Language}", name);

        return Ok(ToInfo(language));
    }

    private static LanguageInfo ToInfo(LanguageDescriptor language) => new()
    {
        Name = language.Name,
        DisplayName = language.DisplayName,
        Version = language.Version,
        Aliases = language.Aliases.ToList(),
        DockerImage = language.DockerImage
    };
}

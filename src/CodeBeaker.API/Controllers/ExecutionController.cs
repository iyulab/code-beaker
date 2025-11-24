using CodeBeaker.API.Models;
using CodeBeaker.Core.Interfaces;
using CodeBeaker.Core.Models;
using CodeBeaker.Runtimes;
using Microsoft.AspNetCore.Mvc;

namespace CodeBeaker.API.Controllers;

/// <summary>
/// 코드 실행 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExecutionController : ControllerBase
{
    private readonly IQueue _queue;
    private readonly IStorage _storage;
    private readonly ILogger<ExecutionController> _logger;

    public ExecutionController(
        IQueue queue,
        IStorage storage,
        ILogger<ExecutionController> logger)
    {
        _queue = queue;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// 코드 실행 요청
    /// </summary>
    /// <param name="request">실행 요청 정보</param>
    /// <returns>실행 ID 및 상태</returns>
    /// <response code="200">요청이 성공적으로 큐에 추가됨</response>
    /// <response code="400">잘못된 요청 (유효성 검증 실패)</response>
    /// <response code="422">지원하지 않는 언어</response>
    [HttpPost]
    [ProducesResponseType(typeof(ExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExecuteResponse>> Execute([FromBody] ExecuteRequest request)
    {
        // 언어 지원 여부 확인
        if (!RuntimeRegistry.IsSupported(request.Language))
        {
            _logger.LogWarning("Unsupported language requested: {Language}", request.Language);
            return UnprocessableEntity(new ErrorResponse
            {
                Code = "UNSUPPORTED_LANGUAGE",
                Message = $"Language '{request.Language}' is not supported. " +
                         $"Supported languages: {string.Join(", ", RuntimeRegistry.GetSupportedLanguages())}"
            });
        }

        try
        {
            // 기본 설정 적용
            var config = request.Config ?? new ExecutionConfig();

            // 큐에 작업 제출
            var executionId = await _queue.SubmitTaskAsync(
                request.Code,
                request.Language,
                config);

            _logger.LogInformation(
                "Execution submitted: {ExecutionId}, Language: {Language}",
                executionId,
                request.Language);

            return Ok(new ExecuteResponse
            {
                ExecutionId = executionId,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit execution request");
            return StatusCode(500, new ErrorResponse
            {
                Code = "INTERNAL_ERROR",
                Message = "Failed to submit execution request"
            });
        }
    }

    /// <summary>
    /// 실행 상태 조회
    /// </summary>
    /// <param name="id">실행 ID</param>
    /// <returns>실행 결과 및 상태</returns>
    /// <response code="200">실행 정보 조회 성공</response>
    /// <response code="404">실행 ID를 찾을 수 없음</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusResponse>> GetStatus(string id)
    {
        try
        {
            var result = await _storage.GetResultAsync(id);

            if (result == null)
            {
                _logger.LogWarning("Execution not found: {ExecutionId}", id);
                return NotFound(new ErrorResponse
                {
                    Code = "NOT_FOUND",
                    Message = $"Execution '{id}' not found"
                });
            }

            return Ok(StatusResponse.FromExecutionResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve execution status: {ExecutionId}", id);
            return StatusCode(500, new ErrorResponse
            {
                Code = "INTERNAL_ERROR",
                Message = "Failed to retrieve execution status"
            });
        }
    }

}

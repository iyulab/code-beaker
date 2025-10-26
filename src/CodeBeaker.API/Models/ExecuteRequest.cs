using System.ComponentModel.DataAnnotations;
using CodeBeaker.Core.Models;

namespace CodeBeaker.API.Models;

/// <summary>
/// 코드 실행 요청
/// </summary>
public sealed class ExecuteRequest
{
    /// <summary>
    /// 실행할 코드
    /// </summary>
    [Required(ErrorMessage = "Code is required")]
    [StringLength(100000, MinimumLength = 1, ErrorMessage = "Code must be between 1 and 100,000 characters")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 프로그래밍 언어 (python, javascript, go, csharp)
    /// </summary>
    [Required(ErrorMessage = "Language is required")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 실행 설정 (선택)
    /// </summary>
    public ExecutionConfig? Config { get; set; }
}

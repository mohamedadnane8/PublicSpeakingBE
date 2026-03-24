using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.API.Controllers;

[ApiController]
[Route("api/resume")]
[Authorize]
public class ResumeController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, HashSet<string>> AllowedContentTypesByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf" },
            [".docx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            }
        };

    private readonly IResumeService _resumeService;
    private readonly IBehavioralQuestionService _behavioralQuestionService;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        IResumeService resumeService,
        IBehavioralQuestionService behavioralQuestionService,
        ILogger<ResumeController> logger)
    {
        _resumeService = resumeService;
        _behavioralQuestionService = behavioralQuestionService;
        _logger = logger;
    }

    [HttpPost("parse")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResumeContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResumeContentDto>> ParseResume(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
            return BadRequest(new { error = "file_required", message = "No file was provided or file is empty." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "file_too_large", message = "File size exceeds the 10 MB limit." });

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypesByExtension.ContainsKey(extension))
            return BadRequest(new { error = "invalid_extension", message = "Only .pdf and .docx files are allowed." });

        var normalizedContentType = file.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedTypes) ||
            !allowedTypes.Contains(normalizedContentType))
        {
            return BadRequest(new
            {
                error = "invalid_content_type",
                message = $"Invalid content type '{file.ContentType}' for extension '{extension}'."
            });
        }

        var userId = GetCurrentUserId();

        try
        {
            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var result = await _resumeService.ParseAndGenerateQuestionsAsync(
                userId, memoryStream, file.FileName, extension, cancellationToken);

            if (!result.Success)
            {
                if (result.RateLimited)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        error = result.Error,
                        message = result.ErrorMessage,
                        uploadsUsed = result.UploadsUsed,
                        maxUploadsPerWeek = result.MaxUploadsPerWeek,
                        nextSlotAt = result.NextSlotAt
                    });
                }

                if (result.Error == "question_generation_failed")
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        error = result.Error,
                        message = result.ErrorMessage
                    });
                }

                return BadRequest(new { error = result.Error, message = result.ErrorMessage });
            }

            memoryStream.Position = 0;
            var dto = new ResumeContentDto(
                FileName: file.FileName,
                ContentType: file.ContentType ?? normalizedContentType,
                PageCount: extension == ".pdf" ? CountPdfPages(memoryStream) : 1,
                QuestionsGenerated: result.QuestionsGenerated,
                DetectedLanguage: result.DetectedLanguage,
                DetectedField: result.DetectedField
            );

            return Ok(dto);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI service call failed during resume parse for user {UserId}", userId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "ai_service_unavailable",
                message = "The AI service is currently unavailable. Please try again later."
            });
        }
    }

    [HttpGet("questions/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var categories = await _resumeService.GetCategoriesAsync(userId, cancellationToken);
        return Ok(categories);
    }

    [HttpGet("questions/random")]
    [ProducesResponseType(typeof(InterviewQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InterviewQuestionDto>> GetRandomQuestion(
        [FromQuery] string? difficulty,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        try
        {
            var question = await _resumeService.GetRandomQuestionAsync(userId, difficulty, category, cancellationToken);
            if (question == null)
            {
                return NotFound(new
                {
                    error = "no_questions",
                    message = "No interview questions found. Please upload a resume first."
                });
            }

            return Ok(question);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_difficulty", message = ex.Message });
        }
    }

    [HttpGet("questions/behavioral/random")]
    [ProducesResponseType(typeof(BehavioralQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BehavioralQuestionDto> GetRandomBehavioralQuestion(
        [FromQuery] string? language,
        [FromQuery] string? difficulty)
    {
        var question = _behavioralQuestionService.GetRandomQuestion(language, difficulty);
        if (question == null)
        {
            return NotFound(new
            {
                error = "no_behavioral_questions",
                message = "No behavioral questions available for the specified language and difficulty."
            });
        }

        return Ok(question);
    }

    private static int CountPdfPages(MemoryStream stream)
    {
        stream.Position = 0;
        using var doc = UglyToad.PdfPig.PdfDocument.Open(stream);
        return doc.NumberOfPages;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new InvalidOperationException("User ID not found in token.");

        return userId;
    }
}

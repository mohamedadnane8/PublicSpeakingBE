using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;
using MyApp.Infrastructure.Configuration;

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
            [".pdf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf"
            },
            [".docx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            }
        };

    private readonly IResumeParserService _resumeParserService;
    private readonly IDeepSeekService _deepSeekService;
    private readonly IInterviewQuestionRepository _questionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBehavioralQuestionService _behavioralQuestionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ResumeUploadOptions _uploadOptions;
    private readonly ILogger<ResumeController> _logger;

    public ResumeController(
        IResumeParserService resumeParserService,
        IDeepSeekService deepSeekService,
        IInterviewQuestionRepository questionRepository,
        IUserRepository userRepository,
        IBehavioralQuestionService behavioralQuestionService,
        IServiceScopeFactory scopeFactory,
        IOptions<ResumeUploadOptions> uploadOptions,
        ILogger<ResumeController> logger)
    {
        _resumeParserService = resumeParserService;
        _deepSeekService = deepSeekService;
        _questionRepository = questionRepository;
        _userRepository = userRepository;
        _behavioralQuestionService = behavioralQuestionService;
        _scopeFactory = scopeFactory;
        _uploadOptions = uploadOptions.Value;
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
        {
            return BadRequest(new { error = "file_required", message = "No file was provided or file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { error = "file_too_large", message = "File size exceeds the 10 MB limit." });
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;

        if (!AllowedContentTypesByExtension.ContainsKey(extension))
        {
            return BadRequest(new
            {
                error = "invalid_extension",
                message = "Only .pdf and .docx files are allowed."
            });
        }

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

        Guid userId;
        try
        {
            userId = GetCurrentUserId();
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { error = "unauthorized", message = "User is not authenticated." });
        }

        // Check weekly upload limit
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            var weekStart = DateTime.UtcNow.AddDays(-7);
            var uploadsThisWeek = user.ResumeUploadHistory.Count(d => d >= weekStart);
            if (uploadsThisWeek >= _uploadOptions.MaxUploadsPerWeek)
            {
                var oldestThisWeek = user.ResumeUploadHistory
                    .Where(d => d >= weekStart)
                    .OrderBy(d => d)
                    .First();
                var nextSlotAt = oldestThisWeek.AddDays(7);

                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    error = "weekly_limit_reached",
                    message = $"You have reached the limit of {_uploadOptions.MaxUploadsPerWeek} resume uploads per week.",
                    uploadsUsed = uploadsThisWeek,
                    maxUploadsPerWeek = _uploadOptions.MaxUploadsPerWeek,
                    nextSlotAt
                });
            }
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var content = extension switch
            {
                ".pdf" => await _resumeParserService.ExtractTextFromPdfAsync(memoryStream),
                ".docx" => await _resumeParserService.ExtractTextFromDocxAsync(memoryStream),
                _ => throw new InvalidOperationException($"Unsupported extension: {extension}")
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new
                {
                    error = "empty_content",
                    message = "Could not extract any text from the uploaded file."
                });
            }

            // Delete old questions for this user
            await _questionRepository.DeleteByUserIdAsync(userId, cancellationToken);

            var questionsPerBatch = _uploadOptions.QuestionsPerBatch;

            // Batch 1: generate first batch synchronously
            var batch1Response = await _deepSeekService.GenerateInterviewQuestionsAsync(
                content, batchNumber: 1, questionsPerBatch: questionsPerBatch, cancellationToken);

            if (batch1Response.Questions.Count == 0)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    error = "question_generation_failed",
                    message = "Failed to generate interview questions from the resume."
                });
            }

            var detectedLanguage = batch1Response.Language ?? "en";
            var detectedField = batch1Response.Field;

            var questions = MapAndCreateEntities(userId, batch1Response.Questions, detectedLanguage);
            await _questionRepository.AddRangeAsync(questions, cancellationToken);
            await _questionRepository.SaveChangesAsync(cancellationToken);

            // Update user: record upload, language, and field
            user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                user.RecordResumeUpload();
                user.SetResumeLanguage(detectedLanguage);
                user.SetDetectedField(detectedField);
                await _userRepository.UpdateAsync(user, cancellationToken);
                await _userRepository.SaveChangesAsync(cancellationToken);
            }

            // Batch 2: fire in background if enabled
            if (_uploadOptions.EnableSecondBatch)
            {
                var resumeText = content;
                var lang = detectedLanguage;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var deepSeek = scope.ServiceProvider.GetRequiredService<IDeepSeekService>();
                        var repo = scope.ServiceProvider.GetRequiredService<IInterviewQuestionRepository>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ResumeController>>();

                        var batch2Response = await deepSeek.GenerateInterviewQuestionsAsync(
                            resumeText, batchNumber: 2, questionsPerBatch: questionsPerBatch);

                        if (batch2Response.Questions.Count > 0)
                        {
                            var batch2Questions = MapAndCreateEntities(userId, batch2Response.Questions, lang);
                            await repo.AddRangeAsync(batch2Questions);
                            await repo.SaveChangesAsync();
                            logger.LogInformation("Batch 2: stored {Count} questions for user {UserId}.", batch2Questions.Count, userId);
                        }
                    }
                    catch (Exception ex)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ResumeController>>();
                        logger.LogError(ex, "Batch 2 question generation failed for user {UserId}.", userId);
                    }
                });
            }

            memoryStream.Position = 0;
            var result = new ResumeContentDto(
                FileName: file.FileName,
                ContentType: file.ContentType ?? normalizedContentType,
                PageCount: extension == ".pdf" ? CountPdfPages(memoryStream) : 1,
                QuestionsGenerated: questions.Count,
                DetectedLanguage: detectedLanguage,
                DetectedField: detectedField
            );

            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DeepSeek API call failed for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "ai_service_unavailable",
                message = "The AI service is currently unavailable. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse resume file '{FileName}' for user {UserId}.", file.FileName, userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "parse_failed",
                message = "Failed to parse the uploaded resume."
            });
        }
    }

    [HttpGet("questions/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = GetCurrentUserId();
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { error = "unauthorized", message = "User is not authenticated." });
        }

        var categories = await _questionRepository.GetCategoriesByUserIdAsync(userId, cancellationToken);
        return Ok(categories);
    }

    [HttpGet("questions/random")]
    [ProducesResponseType(typeof(InterviewQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InterviewQuestionDto>> GetRandomQuestion(
        [FromQuery] string? difficulty,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = GetCurrentUserId();
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { error = "unauthorized", message = "User is not authenticated." });
        }

        QuestionDifficulty? parsedDifficulty = null;
        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            if (Enum.TryParse<QuestionDifficulty>(difficulty, true, out var d))
            {
                parsedDifficulty = d;
            }
            else
            {
                return BadRequest(new
                {
                    error = "invalid_difficulty",
                    message = "Difficulty must be one of: Easy, Medium, Hard."
                });
            }
        }

        var question = await _questionRepository.GetRandomByUserIdAsync(userId, parsedDifficulty, category, cancellationToken);

        if (question == null)
        {
            return NotFound(new
            {
                error = "no_questions",
                message = "No interview questions found. Please upload a resume first."
            });
        }

        return Ok(new InterviewQuestionDto(
            question.Id,
            question.Question,
            question.Category,
            question.Difficulty.ToString(),
            question.ThinkingSeconds,
            question.AnsweringSeconds
        ));
    }

    [HttpGet("questions/behavioral/random")]
    [ProducesResponseType(typeof(BehavioralQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    private static List<InterviewQuestion> MapAndCreateEntities(Guid userId, List<GeneratedQuestionDto> generated, string language)
    {
        return generated.Select(q =>
        {
            var difficulty = Enum.TryParse<QuestionDifficulty>(q.Difficulty, true, out var d)
                ? d
                : QuestionDifficulty.Medium;

            var thinkingSeconds = q.ThinkingSeconds > 0 ? q.ThinkingSeconds : difficulty switch
            {
                QuestionDifficulty.Easy => 20,
                QuestionDifficulty.Hard => 60,
                _ => 40
            };
            var answeringSeconds = q.AnsweringSeconds > 0 ? q.AnsweringSeconds : difficulty switch
            {
                QuestionDifficulty.Easy => 45,
                QuestionDifficulty.Hard => 120,
                _ => 90
            };

            return InterviewQuestion.Create(userId, q.Question, q.Category, difficulty, thinkingSeconds, answeringSeconds, language);
        }).ToList();
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
        {
            throw new InvalidOperationException("User ID not found in token.");
        }

        return userId;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Configuration;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class ResumeService : IResumeService
{
    private readonly IResumeParserService _resumeParserService;
    private readonly IDeepSeekService _deepSeekService;
    private readonly IInterviewQuestionRepository _questionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ResumeUploadOptions _uploadOptions;
    private readonly ILogger<ResumeService> _logger;

    public ResumeService(
        IResumeParserService resumeParserService,
        IDeepSeekService deepSeekService,
        IInterviewQuestionRepository questionRepository,
        IUserRepository userRepository,
        IServiceScopeFactory scopeFactory,
        IOptions<ResumeUploadOptions> uploadOptions,
        ILogger<ResumeService> logger)
    {
        _resumeParserService = resumeParserService;
        _deepSeekService = deepSeekService;
        _questionRepository = questionRepository;
        _userRepository = userRepository;
        _scopeFactory = scopeFactory;
        _uploadOptions = uploadOptions.Value;
        _logger = logger;
    }

    public async Task<ResumeParseResult> ParseAndGenerateQuestionsAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string extension,
        CancellationToken cancellationToken = default)
    {
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

                _logger.LogWarning("Resume upload rate-limited for user {UserId}: {Used}/{Max} this week",
                    userId, uploadsThisWeek, _uploadOptions.MaxUploadsPerWeek);

                return new ResumeParseResult
                {
                    Success = false,
                    RateLimited = true,
                    Error = "weekly_limit_reached",
                    ErrorMessage = $"You have reached the limit of {_uploadOptions.MaxUploadsPerWeek} resume uploads per week.",
                    UploadsUsed = uploadsThisWeek,
                    MaxUploadsPerWeek = _uploadOptions.MaxUploadsPerWeek,
                    NextSlotAt = oldestThisWeek.AddDays(7)
                };
            }
        }

        // Extract text
        _logger.LogInformation("Parsing resume '{FileName}' ({Extension}) for user {UserId}",
            fileName, extension, userId);

        var content = extension switch
        {
            ".pdf" => await _resumeParserService.ExtractTextFromPdfAsync(fileStream),
            ".docx" => await _resumeParserService.ExtractTextFromDocxAsync(fileStream),
            _ => throw new ArgumentException($"Unsupported extension: {extension}")
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty content extracted from resume '{FileName}' for user {UserId}",
                fileName, userId);
            return new ResumeParseResult
            {
                Success = false,
                Error = "empty_content",
                ErrorMessage = "Could not extract any text from the uploaded file."
            };
        }

        _logger.LogInformation("Extracted {CharCount} chars from resume for user {UserId}",
            content.Length, userId);

        // Delete old questions
        await _questionRepository.DeleteByUserIdAsync(userId, cancellationToken);

        // Batch 1: generate synchronously
        var questionsPerBatch = _uploadOptions.QuestionsPerBatch;

        _logger.LogInformation("Generating batch 1 questions ({Count} per batch) for user {UserId}",
            questionsPerBatch, userId);

        var batch1Response = await _deepSeekService.GenerateInterviewQuestionsAsync(
            content, batchNumber: 1, questionsPerBatch: questionsPerBatch, cancellationToken);

        if (batch1Response.Questions.Count == 0)
        {
            _logger.LogWarning("DeepSeek returned 0 questions for user {UserId} batch 1", userId);
            return new ResumeParseResult
            {
                Success = false,
                Error = "question_generation_failed",
                ErrorMessage = "Failed to generate interview questions from the resume."
            };
        }

        var detectedLanguage = batch1Response.Language ?? "en";
        var detectedField = batch1Response.Field;

        var questions = MapAndCreateEntities(userId, batch1Response.Questions, detectedLanguage);
        await _questionRepository.AddRangeAsync(questions, cancellationToken);
        await _questionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stored {Count} batch 1 questions for user {UserId} (language={Language}, field={Field})",
            questions.Count, userId, detectedLanguage, detectedField);

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
                    var bgLogger = scope.ServiceProvider.GetRequiredService<ILogger<ResumeService>>();

                    bgLogger.LogInformation("Starting batch 2 question generation for user {UserId}", userId);

                    var batch2Response = await deepSeek.GenerateInterviewQuestionsAsync(
                        resumeText, batchNumber: 2, questionsPerBatch: questionsPerBatch);

                    if (batch2Response.Questions.Count > 0)
                    {
                        var batch2Questions = MapAndCreateEntities(userId, batch2Response.Questions, lang);
                        await repo.AddRangeAsync(batch2Questions);
                        await repo.SaveChangesAsync();
                        bgLogger.LogInformation("Stored {Count} batch 2 questions for user {UserId}",
                            batch2Questions.Count, userId);
                    }
                    else
                    {
                        bgLogger.LogWarning("DeepSeek returned 0 questions for user {UserId} batch 2", userId);
                    }
                }
                catch (Exception ex)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var bgLogger = scope.ServiceProvider.GetRequiredService<ILogger<ResumeService>>();
                    bgLogger.LogError(ex, "Batch 2 question generation failed for user {UserId}", userId);
                }
            });
        }

        return new ResumeParseResult
        {
            Success = true,
            QuestionsGenerated = questions.Count,
            DetectedLanguage = detectedLanguage,
            DetectedField = detectedField
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _questionRepository.GetCategoriesByUserIdAsync(userId, cancellationToken);
    }

    public async Task<InterviewQuestionDto?> GetRandomQuestionAsync(
        Guid userId, string? difficulty, string? category,
        CancellationToken cancellationToken = default)
    {
        QuestionDifficulty? parsedDifficulty = null;
        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            if (!Enum.TryParse<QuestionDifficulty>(difficulty, true, out var d))
                throw new ArgumentException("Difficulty must be one of: Easy, Medium, Hard.");
            parsedDifficulty = d;
        }

        var question = await _questionRepository.GetRandomByUserIdAsync(
            userId, parsedDifficulty, category, cancellationToken);

        if (question == null)
            return null;

        return new InterviewQuestionDto(
            question.Id,
            question.Question,
            question.Category,
            question.Difficulty.ToString(),
            question.ThinkingSeconds,
            question.AnsweringSeconds);
    }

    private static List<InterviewQuestion> MapAndCreateEntities(
        Guid userId, List<GeneratedQuestionDto> generated, string language)
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

            return InterviewQuestion.Create(userId, q.Question, q.Category, difficulty,
                thinkingSeconds, answeringSeconds, language);
        }).ToList();
    }
}

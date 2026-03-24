using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IResumeService
{
    Task<ResumeParseResult> ParseAndGenerateQuestionsAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string extension,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InterviewQuestionDto?> GetRandomQuestionAsync(
        Guid userId,
        string? difficulty,
        string? category,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from parsing a resume and generating questions.
/// </summary>
public class ResumeParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorMessage { get; set; }
    public int QuestionsGenerated { get; set; }
    public string DetectedLanguage { get; set; } = "en";
    public string? DetectedField { get; set; }

    /// <summary>True when the user has hit the weekly upload cap.</summary>
    public bool RateLimited { get; set; }
    public int? UploadsUsed { get; set; }
    public int? MaxUploadsPerWeek { get; set; }
    public DateTime? NextSlotAt { get; set; }
}

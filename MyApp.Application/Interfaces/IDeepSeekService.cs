using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IDeepSeekService
{
    Task<List<GeneratedQuestionDto>> GenerateInterviewQuestionsAsync(
        string resumeText,
        int batchNumber,
        int questionsPerBatch,
        CancellationToken cancellationToken = default);
}

using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IDeepSeekService
{
    Task<DeepSeekResponseDto> GenerateInterviewQuestionsAsync(
        string resumeText,
        int batchNumber,
        int questionsPerBatch,
        CancellationToken cancellationToken = default);
}

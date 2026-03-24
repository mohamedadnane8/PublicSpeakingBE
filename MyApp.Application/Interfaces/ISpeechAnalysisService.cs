using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface ISpeechAnalysisService
{
    /// <summary>
    /// Analyze a general speech transcript and return detailed scoring.
    /// </summary>
    Task<GeneralSpeechAnalysisDto> AnalyzeGeneralSpeechAsync(
        string transcript,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze an interview answer transcript and return STAR-framework scoring.
    /// </summary>
    Task<InterviewSpeechAnalysisDto> AnalyzeInterviewSpeechAsync(
        string question,
        string transcript,
        CancellationToken cancellationToken = default);
}

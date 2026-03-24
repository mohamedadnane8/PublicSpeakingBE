using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateSessionAsync(Guid userId, CreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<SessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SetSpeechAnalysisAsync(Guid sessionId, string analysisJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze a session's transcript. Returns cached result if available unless reanalyze is true.
    /// Enforces daily rate limit for fresh AI analyses.
    /// </summary>
    Task<SessionAnalysisResult> AnalyzeSessionAsync(Guid sessionId, Guid userId, bool reanalyze = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a pre-signed URL for session audio playback.
    /// </summary>
    Task<AudioPlaybackUrlDto?> GetAudioUrlAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a session analysis attempt — covers success, cached, rate-limited, and error cases.
/// </summary>
public class SessionAnalysisResult
{
    public bool Success { get; set; }
    public SpeechAnalysisResultDto? Analysis { get; set; }

    // Error info
    public string? Error { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatus { get; set; }

    // Rate limit info
    public bool RateLimited { get; set; }
    public int? AnalysesUsed { get; set; }
    public int? MaxPerDay { get; set; }
    public DateTime? ResetsAt { get; set; }
}

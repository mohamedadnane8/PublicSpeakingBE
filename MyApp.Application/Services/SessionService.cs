using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;

    public SessionService(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<SessionDto> CreateSessionAsync(
        Guid userId, 
        CreateSessionRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Parse enums
        var mode = ParseEnum<SessionMode>(request.Mode);
        var language = ParseEnum<SessionLanguage>(request.Language);
        var difficulty = ParseEnum<SessionDifficulty>(request.Difficulty);
        var status = ParseEnum<SessionStatus>(request.Status);
        var cancelReason = string.IsNullOrEmpty(request.CancelReason) 
            ? (CancelReason?)null 
            : ParseEnum<CancelReason>(request.CancelReason);

        // Create session
        var session = Session.Create(
            request.Id,
            userId,
            request.CreatedAt,
            mode,
            language,
            difficulty,
            request.Word,
            request.ThinkSeconds,
            request.SpeakSeconds);

        // Set status
        session.SetStatus(status, cancelReason);

        // Set completed at if provided
        if (request.CompletedAt.HasValue)
        {
            session.SetCompletedAt(request.CompletedAt.Value);
        }

        // Set ratings if provided
        if (request.Ratings != null)
        {
            session.SetRatings(
                request.Ratings.Opening,
                request.Ratings.Structure,
                request.Ratings.Ending,
                request.Ratings.Confidence,
                request.Ratings.Clarity,
                request.Ratings.Authenticity,
                request.Ratings.LanguageExpression);
        }

        // Set overall score
        session.SetOverallScore(request.OverallScore);

        // Set notes
        session.SetNotes(request.Notes);

        // Set audio metadata
        if (request.Audio != null)
        {
            AudioErrorCode? audioError = string.IsNullOrEmpty(request.Audio.ErrorCode)
                ? (AudioErrorCode?)null
                : ParseEnum<AudioErrorCode>(request.Audio.ErrorCode);

            session.SetAudioMetadata(
                request.Audio.Available,
                request.Audio.DurationMs,
                request.Audio.RecordingStartedAt,
                request.Audio.RecordingEndedAt,
                audioError);
        }

        // Set transcript
        session.SetTranscript(request.Transcript);

        // Save to database
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(session);
    }

    public async Task<SessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        return session == null ? null : MapToDto(session);
    }

    public async Task<IReadOnlyList<SessionDto>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetByUserIdAsync(userId, cancellationToken);
        return sessions.Select(MapToDto).ToList();
    }

    private static SessionDto MapToDto(Session session)
    {
        return new SessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            Mode = session.Mode.ToString(),
            Language = session.Language.ToString().ToUpperInvariant(),
            Difficulty = session.Difficulty.ToString().ToUpperInvariant(),
            Word = session.Word,
            ThinkSeconds = session.ThinkSeconds,
            SpeakSeconds = session.SpeakSeconds,
            Status = session.Status.ToString(),
            CancelReason = session.CancelReason?.ToString(),
            Ratings = new SessionRatingsDto
            {
                Opening = session.RatingOpening,
                Structure = session.RatingStructure,
                Ending = session.RatingEnding,
                Confidence = session.RatingConfidence,
                Clarity = session.RatingClarity,
                Authenticity = session.RatingAuthenticity,
                LanguageExpression = session.RatingLanguageExpression
            },
            OverallScore = session.OverallScore,
            Notes = session.Notes,
            Audio = new SessionAudioDto
            {
                Available = session.AudioAvailable,
                DurationMs = session.AudioDurationMs,
                RecordingStartedAt = session.AudioRecordingStartedAt,
                RecordingEndedAt = session.AudioRecordingEndedAt,
                ErrorCode = session.AudioErrorCode?.ToString()
            },
            Transcript = session.Transcript
        };
    }

    private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct
    {
        // Convert PascalCase to match our enum names
        // e.g., "EXPLANATION" -> "Explanation"
        var normalizedValue = char.ToUpper(value[0]) + value[1..].ToLowerInvariant();
        
        if (Enum.TryParse<TEnum>(normalizedValue, true, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid value '{value}' for enum {typeof(TEnum).Name}");
    }
}

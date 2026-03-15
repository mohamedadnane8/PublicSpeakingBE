using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities;

/// <summary>
/// Public Speaking Session entity
/// </summary>
public class Session
{
    // Identifiers
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Session Configuration
    public SessionMode Mode { get; private set; }
    public string Word { get; private set; } = null!;

    // Timing Settings
    public int ThinkSeconds { get; private set; }
    public int SpeakSeconds { get; private set; }

    // Status
    public SessionStatus Status { get; private set; }
    public CancelReason? CancelReason { get; private set; }

    // Self-Ratings (1-5 scale)
    public int? RatingOpening { get; private set; }
    public int? RatingStructure { get; private set; }
    public int? RatingEnding { get; private set; }
    public int? RatingConfidence { get; private set; }
    public int? RatingClarity { get; private set; }
    public int? RatingAuthenticity { get; private set; }
    public int? RatingLanguageExpression { get; private set; }

    // Calculated Score
    public decimal? OverallScore { get; private set; }

    // Notes
    public string? Notes { get; private set; }

    // Audio Metadata
    public bool AudioAvailable { get; private set; }
    public int? AudioDurationMs { get; private set; }
    public DateTime? AudioRecordingStartedAt { get; private set; }
    public DateTime? AudioRecordingEndedAt { get; private set; }
    public AudioErrorCode? AudioErrorCode { get; private set; }

    // Transcript
    public string? Transcript { get; private set; }

    // Navigation property
    public User User { get; private set; } = null!;

    // EF Core requires a parameterless constructor
    private Session() { }

    public static Session Create(
        Guid id,
        Guid userId,
        DateTime createdAt,
        SessionMode mode,
        string word,
        int thinkSeconds,
        int speakSeconds)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id is required", nameof(id));
        
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));
        
        if (string.IsNullOrWhiteSpace(word))
            throw new ArgumentException("Word is required", nameof(word));
        
        if (thinkSeconds <= 0)
            throw new ArgumentException("Think seconds must be positive", nameof(thinkSeconds));
        
        if (speakSeconds <= 0)
            throw new ArgumentException("Speak seconds must be positive", nameof(speakSeconds));

        return new Session
        {
            Id = id,
            UserId = userId,
            CreatedAt = createdAt,
            Mode = mode,
            Word = word,
            ThinkSeconds = thinkSeconds,
            SpeakSeconds = speakSeconds,
            Status = SessionStatus.Completed // Default, will be updated
        };
    }

    public void SetStatus(SessionStatus status, CancelReason? cancelReason = null)
    {
        Status = status;
        
        if (status != SessionStatus.Completed && cancelReason.HasValue)
        {
            CancelReason = cancelReason;
        }
    }

    public void SetCompletedAt(DateTime completedAt)
    {
        CompletedAt = completedAt;
    }

    public void SetRatings(
        int? opening,
        int? structure,
        int? ending,
        int? confidence,
        int? clarity,
        int? authenticity,
        int? languageExpression)
    {
        ValidateRating(opening, nameof(opening));
        ValidateRating(structure, nameof(structure));
        ValidateRating(ending, nameof(ending));
        ValidateRating(confidence, nameof(confidence));
        ValidateRating(clarity, nameof(clarity));
        ValidateRating(authenticity, nameof(authenticity));
        ValidateRating(languageExpression, nameof(languageExpression));

        RatingOpening = opening;
        RatingStructure = structure;
        RatingEnding = ending;
        RatingConfidence = confidence;
        RatingClarity = clarity;
        RatingAuthenticity = authenticity;
        RatingLanguageExpression = languageExpression;
    }

    public void SetOverallScore(decimal? score)
    {
        if (score.HasValue && (score < 0 || score > 10))
            throw new ArgumentException("Overall score must be between 0 and 10", nameof(score));
        
        OverallScore = score;
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
    }

    public void SetAudioMetadata(
        bool available,
        int? durationMs = null,
        DateTime? recordingStartedAt = null,
        DateTime? recordingEndedAt = null,
        AudioErrorCode? errorCode = null)
    {
        AudioAvailable = available;
        AudioDurationMs = durationMs;
        AudioRecordingStartedAt = recordingStartedAt;
        AudioRecordingEndedAt = recordingEndedAt;
        AudioErrorCode = errorCode;
    }

    public void SetTranscript(string? transcript)
    {
        Transcript = transcript;
    }

    private static void ValidateRating(int? rating, string name)
    {
        if (rating.HasValue && (rating < 1 || rating > 5))
            throw new ArgumentException($"{name} must be between 1 and 5", name);
    }
}

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
    public SessionType Type { get; private set; }
    public SessionLanguage Language { get; private set; }
    public SessionDifficulty Difficulty { get; private set; }
    public string Word { get; private set; } = null!;

    // Timing Settings
    public int ThinkSeconds { get; private set; }
    public int SpeakSeconds { get; private set; }

    // Status
    public SessionStatus Status { get; private set; }
    public CancelReason? CancelReason { get; private set; }

    // === General Speech Ratings (1-5 scale) ===
    public int? RatingOpening { get; private set; }
    public int? RatingStructure { get; private set; }
    public int? RatingEnding { get; private set; }
    public int? RatingConfidence { get; private set; }
    public int? RatingClarity { get; private set; }
    public int? RatingAuthenticity { get; private set; }
    public int? RatingLanguageExpression { get; private set; }
    public int? RatingPassion { get; private set; } // 5% bonus

    // === Interview Speech Ratings (1-5 scale, STAR framework) ===
    public int? RatingRelevance { get; private set; }
    public int? RatingSituationStakes { get; private set; }
    public int? RatingAction { get; private set; }
    public int? RatingResultImpact { get; private set; }
    public int? RatingDeliveryComposure { get; private set; }
    public int? RatingConciseness { get; private set; }

    // Notes
    public string? Notes { get; private set; }

    // Audio Metadata
    public bool AudioAvailable { get; private set; }
    public int? AudioDurationMs { get; private set; }
    public DateTime? AudioRecordingStartedAt { get; private set; }
    public DateTime? AudioRecordingEndedAt { get; private set; }
    public AudioErrorCode? AudioErrorCode { get; private set; }
    public string? AudioObjectKey { get; private set; }
    public string? AudioBucketName { get; private set; }
    public string? AudioRegion { get; private set; }
    public DateTime? AudioUploadedAt { get; private set; }

    // Transcript
    public string? Transcript { get; private set; }
    public string? TranscriptionStatus { get; private set; }
    public string? TranscriptionError { get; private set; }

    // Speech Analysis (stored as JSON)
    public string? SpeechAnalysis { get; private set; }
    public DateTime? AnalyzedAt { get; private set; }
    public bool AiScored { get; private set; }

    // Navigation property
    public User User { get; private set; } = null!;

    private Session() { }

    public static Session Create(
        Guid id,
        Guid userId,
        DateTime createdAt,
        SessionMode mode,
        SessionLanguage language,
        SessionDifficulty difficulty,
        string word,
        int thinkSeconds,
        int speakSeconds,
        SessionType type = SessionType.General)
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
            Language = language,
            Difficulty = difficulty,
            Word = word,
            ThinkSeconds = thinkSeconds,
            SpeakSeconds = speakSeconds,
            Type = type,
            Status = SessionStatus.Completed
        };
    }

    public void SetStatus(SessionStatus status, CancelReason? cancelReason = null)
    {
        Status = status;
        if (status != SessionStatus.Completed && cancelReason.HasValue)
            CancelReason = cancelReason;
    }

    public void SetCompletedAt(DateTime completedAt) => CompletedAt = completedAt;

    /// <summary>
    /// Set General Speech ratings (7 criteria + Passion bonus).
    /// </summary>
    public void SetGeneralRatings(
        int? opening, int? structure, int? ending,
        int? confidence, int? clarity, int? authenticity,
        int? languageExpression, int? passion = null)
    {
        ValidateRating(opening, nameof(opening));
        ValidateRating(structure, nameof(structure));
        ValidateRating(ending, nameof(ending));
        ValidateRating(confidence, nameof(confidence));
        ValidateRating(clarity, nameof(clarity));
        ValidateRating(authenticity, nameof(authenticity));
        ValidateRating(languageExpression, nameof(languageExpression));
        ValidateRating(passion, nameof(passion));

        RatingOpening = opening;
        RatingStructure = structure;
        RatingEnding = ending;
        RatingConfidence = confidence;
        RatingClarity = clarity;
        RatingAuthenticity = authenticity;
        RatingLanguageExpression = languageExpression;
        RatingPassion = passion;
    }

    /// <summary>
    /// Set Interview Speech ratings (STAR framework, 6 criteria).
    /// </summary>
    public void SetInterviewRatings(
        int? relevance, int? situationStakes, int? action,
        int? resultImpact, int? deliveryComposure, int? conciseness)
    {
        ValidateRating(relevance, nameof(relevance));
        ValidateRating(situationStakes, nameof(situationStakes));
        ValidateRating(action, nameof(action));
        ValidateRating(resultImpact, nameof(resultImpact));
        ValidateRating(deliveryComposure, nameof(deliveryComposure));
        ValidateRating(conciseness, nameof(conciseness));

        RatingRelevance = relevance;
        RatingSituationStakes = situationStakes;
        RatingAction = action;
        RatingResultImpact = resultImpact;
        RatingDeliveryComposure = deliveryComposure;
        RatingConciseness = conciseness;
    }

    /// <summary>Backward compat for existing callers.</summary>
    public void SetRatings(
        int? opening, int? structure, int? ending,
        int? confidence, int? clarity, int? authenticity,
        int? languageExpression)
    {
        SetGeneralRatings(opening, structure, ending, confidence, clarity, authenticity, languageExpression);
    }

    public void SetNotes(string? notes) => Notes = notes;

    public void SetAudioMetadata(
        bool available, int? durationMs = null,
        DateTime? recordingStartedAt = null, DateTime? recordingEndedAt = null,
        AudioErrorCode? errorCode = null, string? objectKey = null,
        string? bucketName = null, string? region = null,
        DateTime? uploadedAt = null)
    {
        AudioAvailable = available;
        AudioDurationMs = durationMs;
        AudioRecordingStartedAt = recordingStartedAt;
        AudioRecordingEndedAt = recordingEndedAt;
        AudioErrorCode = errorCode;
        AudioObjectKey = string.IsNullOrWhiteSpace(objectKey) ? null : objectKey.Trim();
        AudioBucketName = string.IsNullOrWhiteSpace(bucketName) ? null : bucketName.Trim();
        AudioRegion = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        AudioUploadedAt = uploadedAt;
    }

    public void SetTranscript(string? transcript) => Transcript = transcript;

    public void SetTranscriptionStatus(string? status, string? error = null)
    {
        TranscriptionStatus = status;
        TranscriptionError = error;
    }

    public void SetSpeechAnalysis(string analysisJson)
    {
        SpeechAnalysis = analysisJson;
        AnalyzedAt = DateTime.UtcNow;
        AiScored = true;
    }

    private static void ValidateRating(int? rating, string name)
    {
        if (rating.HasValue && (rating < 1 || rating > 5))
            throw new ArgumentException($"{name} must be between 1 and 5", name);
    }
}

using MyApp.Domain.Enums;

namespace MyApp.Application.DTOs;

// Request DTO for creating a session
public class CreateSessionRequest
{
    // Identifiers
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Session Configuration
    public string Mode { get; set; } = null!;
    public string Type { get; set; } = "General";
    public string Language { get; set; } = "EN";
    public string Difficulty { get; set; } = "MEDIUM";
    public string Word { get; set; } = null!;

    // Timing Settings
    public int ThinkSeconds { get; set; }
    public int SpeakSeconds { get; set; }

    // Status
    public string Status { get; set; } = null!;
    public string? CancelReason { get; set; }

    // Self-Ratings (use GeneralRatings for General, InterviewRatings for Interview)
    public GeneralRatingsDto? Ratings { get; set; }
    public InterviewRatingsDto? InterviewRatings { get; set; }

    // Notes
    public string? Notes { get; set; }

    // Audio Metadata
    public SessionAudioDto? Audio { get; set; }

    // Transcript
    public string? Transcript { get; set; }
}

/// <summary>
/// General Speech ratings (7 criteria + Passion bonus).
/// Weights: Opening 15%, Structure 20%, Ending 15%, Confidence 15%,
///          Clarity 15%, Authenticity 10%, Language 10%, Passion 5% bonus
/// </summary>
public class GeneralRatingsDto
{
    public int? Opening { get; set; }
    public int? Structure { get; set; }
    public int? Ending { get; set; }
    public int? Confidence { get; set; }
    public int? Clarity { get; set; }
    public int? Authenticity { get; set; }
    public int? LanguageExpression { get; set; }
    public int? Passion { get; set; } // Bonus
}

/// <summary>
/// Interview Speech ratings (STAR framework, 6 criteria).
/// Weights: Relevance 20% (gating), Situation 15%, Action 30%,
///          Result 20%, Delivery 10%, Conciseness 5%
/// Gating: if Relevance=1, max total capped at 4/10
/// </summary>
public class InterviewRatingsDto
{
    public int? Relevance { get; set; }
    public int? SituationStakes { get; set; }
    public int? Action { get; set; }
    public int? ResultImpact { get; set; }
    public int? DeliveryComposure { get; set; }
    public int? Conciseness { get; set; }
}

public class SessionAudioDto
{
    public bool Available { get; set; }
    public int? DurationMs { get; set; }
    public DateTime? RecordingStartedAt { get; set; }
    public DateTime? RecordingEndedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ObjectKey { get; set; }
    public string? BucketName { get; set; }
    public string? Region { get; set; }
    public DateTime? UploadedAt { get; set; }
}

// Response DTO
public class SessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string Mode { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Language { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
    public string Word { get; set; } = null!;

    public int ThinkSeconds { get; set; }
    public int SpeakSeconds { get; set; }

    public string Status { get; set; } = null!;
    public string? CancelReason { get; set; }

    // General speech ratings (null for interview sessions)
    public GeneralRatingsDto? Ratings { get; set; }
    // Interview speech ratings (null for general sessions)
    public InterviewRatingsDto? InterviewRatings { get; set; }

    // Calculated scores (not stored, computed from ratings)
    public decimal? ManualScore { get; set; }
    public decimal? AiScore { get; set; }
    public string? Notes { get; set; }

    public SessionAudioDto? Audio { get; set; }
    public string? Transcript { get; set; }
    public string? TranscriptionStatus { get; set; }
    public string? TranscriptionError { get; set; }
    public string? Advice { get; set; }

    // Speech analysis (raw JSON from DeepSeek, null if not yet analyzed)
    public object? SpeechAnalysis { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public bool AiScored { get; set; }
}

/// <summary>
/// Request DTO for updating an existing session (ratings, notes, status).
/// Only non-null fields are applied.
/// </summary>
public class UpdateSessionRequest
{
    public string? Status { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public GeneralRatingsDto? Ratings { get; set; }
    public InterviewRatingsDto? InterviewRatings { get; set; }
    public string? Notes { get; set; }
}

// Backward compat alias
public class SessionRatingsDto : GeneralRatingsDto { }

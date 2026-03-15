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
    public string Word { get; set; } = null!;

    // Timing Settings
    public int ThinkSeconds { get; set; }
    public int SpeakSeconds { get; set; }

    // Status
    public string Status { get; set; } = null!;
    public string? CancelReason { get; set; }

    // Self-Ratings
    public SessionRatingsDto? Ratings { get; set; }

    // Calculated Score
    public decimal? OverallScore { get; set; }

    // Notes
    public string? Notes { get; set; }

    // Audio Metadata
    public SessionAudioDto? Audio { get; set; }

    // Transcript
    public string? Transcript { get; set; }
}

public class SessionRatingsDto
{
    public int? Opening { get; set; }
    public int? Structure { get; set; }
    public int? Ending { get; set; }
    public int? Confidence { get; set; }
    public int? Clarity { get; set; }
    public int? Authenticity { get; set; }
    public int? LanguageExpression { get; set; }
}

public class SessionAudioDto
{
    public bool Available { get; set; }
    public int? DurationMs { get; set; }
    public DateTime? RecordingStartedAt { get; set; }
    public DateTime? RecordingEndedAt { get; set; }
    public string? ErrorCode { get; set; }
}

// Response DTO
public class SessionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string Mode { get; set; } = null!;
    public string Word { get; set; } = null!;

    public int ThinkSeconds { get; set; }
    public int SpeakSeconds { get; set; }

    public string Status { get; set; } = null!;
    public string? CancelReason { get; set; }

    public SessionRatingsDto? Ratings { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Notes { get; set; }

    public SessionAudioDto? Audio { get; set; }
    public string? Transcript { get; set; }
}

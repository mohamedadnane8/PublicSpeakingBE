using System.Text.Json.Serialization;

namespace MyApp.Application.DTOs;

// === Shared ===
public class FillerAnalysisDto
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("estimated_duration_minutes")]
    public double EstimatedDurationMinutes { get; set; }

    [JsonPropertyName("filler_rate_per_minute")]
    public double FillerRatePerMinute { get; set; }

    [JsonPropertyName("rate_label")]
    public string RateLabel { get; set; } = null!;

    [JsonPropertyName("clustering_note")]
    public string? ClusteringNote { get; set; }

    [JsonPropertyName("breakdown")]
    public FillerBreakdownDto Breakdown { get; set; } = null!;

    [JsonPropertyName("top_offenders")]
    public List<string> TopOffenders { get; set; } = [];
}

public class FillerBreakdownDto
{
    [JsonPropertyName("hesitation_sounds")]
    public FillerCategoryDto HesitationSounds { get; set; } = null!;

    [JsonPropertyName("padding_phrases")]
    public FillerCategoryDto PaddingPhrases { get; set; } = null!;

    [JsonPropertyName("verbal_tics")]
    public FillerCategoryDto VerbalTics { get; set; } = null!;

    [JsonPropertyName("restarts")]
    public RestartsDto Restarts { get; set; } = null!;
}

public class FillerCategoryDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("words")]
    public Dictionary<string, int> Words { get; set; } = new();
}

public class RestartsDto
{
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// What DeepSeek returns per criterion: raw score (1-5) + feedback only.
/// Weighted score is calculated server-side.
/// </summary>
public class RawCriterionScoreDto
{
    [JsonPropertyName("raw")]
    public int Raw { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = null!;
}

/// <summary>
/// Full criterion score returned to frontend: raw + weighted (calculated server-side) + feedback.
/// </summary>
public class CriterionScoreDto
{
    [JsonPropertyName("raw")]
    public int Raw { get; set; }

    [JsonPropertyName("weighted")]
    public double Weighted { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = null!;
}

public class PassionScoreDto
{
    [JsonPropertyName("raw")]
    public int Raw { get; set; }

    [JsonPropertyName("bonus_points")]
    public double BonusPoints { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = null!;
}

// === Raw AI Response (what DeepSeek returns — no math) ===

public class RawGeneralScoresDto
{
    [JsonPropertyName("opening")]
    public RawCriterionScoreDto Opening { get; set; } = null!;

    [JsonPropertyName("structure")]
    public RawCriterionScoreDto Structure { get; set; } = null!;

    [JsonPropertyName("closing")]
    public RawCriterionScoreDto Closing { get; set; } = null!;

    [JsonPropertyName("confidence")]
    public RawCriterionScoreDto Confidence { get; set; } = null!;

    [JsonPropertyName("clarity")]
    public RawCriterionScoreDto Clarity { get; set; } = null!;

    [JsonPropertyName("authenticity")]
    public RawCriterionScoreDto Authenticity { get; set; } = null!;

    [JsonPropertyName("language")]
    public RawCriterionScoreDto Language { get; set; } = null!;

    [JsonPropertyName("passion")]
    public RawCriterionScoreDto Passion { get; set; } = null!;
}

public class RawInterviewScoresDto
{
    [JsonPropertyName("relevance")]
    public RawCriterionScoreDto Relevance { get; set; } = null!;

    [JsonPropertyName("situation")]
    public RawCriterionScoreDto Situation { get; set; } = null!;

    [JsonPropertyName("action")]
    public RawCriterionScoreDto Action { get; set; } = null!;

    [JsonPropertyName("result")]
    public RawCriterionScoreDto Result { get; set; } = null!;

    [JsonPropertyName("delivery")]
    public RawCriterionScoreDto Delivery { get; set; } = null!;

    [JsonPropertyName("conciseness")]
    public RawCriterionScoreDto Conciseness { get; set; } = null!;
}

public class RawGeneralAnalysisDto
{
    [JsonPropertyName("filler_analysis")]
    public FillerAnalysisDto FillerAnalysis { get; set; } = null!;

    [JsonPropertyName("scores")]
    public RawGeneralScoresDto Scores { get; set; } = null!;

    [JsonPropertyName("top_strength")]
    public string TopStrength { get; set; } = null!;

    [JsonPropertyName("top_improvement")]
    public string TopImprovement { get; set; } = null!;
}

public class RawInterviewAnalysisDto
{
    [JsonPropertyName("filler_analysis")]
    public FillerAnalysisDto FillerAnalysis { get; set; } = null!;

    [JsonPropertyName("scores")]
    public RawInterviewScoresDto Scores { get; set; } = null!;

    [JsonPropertyName("top_strength")]
    public string TopStrength { get; set; } = null!;

    [JsonPropertyName("top_improvement")]
    public string TopImprovement { get; set; } = null!;
}

// === Computed Response (returned to frontend — includes server-calculated math) ===

public class GeneralScoresDto
{
    [JsonPropertyName("opening")]
    public CriterionScoreDto Opening { get; set; } = null!;

    [JsonPropertyName("structure")]
    public CriterionScoreDto Structure { get; set; } = null!;

    [JsonPropertyName("closing")]
    public CriterionScoreDto Closing { get; set; } = null!;

    [JsonPropertyName("confidence")]
    public CriterionScoreDto Confidence { get; set; } = null!;

    [JsonPropertyName("clarity")]
    public CriterionScoreDto Clarity { get; set; } = null!;

    [JsonPropertyName("authenticity")]
    public CriterionScoreDto Authenticity { get; set; } = null!;

    [JsonPropertyName("language")]
    public CriterionScoreDto Language { get; set; } = null!;

    [JsonPropertyName("passion")]
    public PassionScoreDto Passion { get; set; } = null!;
}

public class InterviewScoresDto
{
    [JsonPropertyName("relevance")]
    public CriterionScoreDto Relevance { get; set; } = null!;

    [JsonPropertyName("situation")]
    public CriterionScoreDto Situation { get; set; } = null!;

    [JsonPropertyName("action")]
    public CriterionScoreDto Action { get; set; } = null!;

    [JsonPropertyName("result")]
    public CriterionScoreDto Result { get; set; } = null!;

    [JsonPropertyName("delivery")]
    public CriterionScoreDto Delivery { get; set; } = null!;

    [JsonPropertyName("conciseness")]
    public CriterionScoreDto Conciseness { get; set; } = null!;
}

public class GeneralSpeechAnalysisDto
{
    [JsonPropertyName("filler_analysis")]
    public FillerAnalysisDto FillerAnalysis { get; set; } = null!;

    [JsonPropertyName("scores")]
    public GeneralScoresDto Scores { get; set; } = null!;

    [JsonPropertyName("base_score")]
    public double BaseScore { get; set; }

    [JsonPropertyName("total_score")]
    public double TotalScore { get; set; }

    [JsonPropertyName("band")]
    public string Band { get; set; } = null!;

    [JsonPropertyName("top_strength")]
    public string TopStrength { get; set; } = null!;

    [JsonPropertyName("top_improvement")]
    public string TopImprovement { get; set; } = null!;
}

public class InterviewSpeechAnalysisDto
{
    [JsonPropertyName("filler_analysis")]
    public FillerAnalysisDto FillerAnalysis { get; set; } = null!;

    [JsonPropertyName("scores")]
    public InterviewScoresDto Scores { get; set; } = null!;

    [JsonPropertyName("gate_applied")]
    public bool GateApplied { get; set; }

    [JsonPropertyName("base_score")]
    public double BaseScore { get; set; }

    [JsonPropertyName("total_score")]
    public double TotalScore { get; set; }

    [JsonPropertyName("band")]
    public string Band { get; set; } = null!;

    [JsonPropertyName("top_strength")]
    public string TopStrength { get; set; } = null!;

    [JsonPropertyName("top_improvement")]
    public string TopImprovement { get; set; } = null!;
}

// === Wrapper returned by the endpoint ===
public class SpeechAnalysisResultDto
{
    public string SessionType { get; set; } = null!;
    public GeneralSpeechAnalysisDto? GeneralAnalysis { get; set; }
    public InterviewSpeechAnalysisDto? InterviewAnalysis { get; set; }
}

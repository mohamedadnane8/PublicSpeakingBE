using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class SessionService : ISessionService
{
    private sealed record AdviceTemplate(
        string Label,
        string[] Strength,
        string[] Improve,
        string[] Priority,
        string[] Polish);

    private sealed record RatedArea(
        string Key,
        int Score,
        AdviceTemplate Template);

    private enum AdviceTone
    {
        Strong,
        Solid,
        ImprovementFocused
    }

    private static readonly IReadOnlyDictionary<string, AdviceTemplate> AdviceTemplates =
        new Dictionary<string, AdviceTemplate>(StringComparer.OrdinalIgnoreCase)
{
    ["opening"] = new AdviceTemplate(
        Label: "opening",
        Strength:
        [
            "You begin with a clear first idea.",
            "Your opening moves quickly to the point.",
            "You start in a way that sets direction for the answer."
        ],
        Improve:
        [
            "Start with a clearer first idea instead of circling the topic.",
            "Use a short framing sentence to anchor the beginning.",
            "State your angle earlier so the listener knows your direction.",
            "Open with a concrete thought rather than a vague introduction.",
            "Begin with a simple statement that leads into your main point."
        ],
        Priority:
        [
            "Your opening needs a clearer first idea.",
            "Start faster and make the first sentence decisive.",
            "Give the listener a strong entry into your answer."
        ],
        Polish:
        [
            "Make the first sentence even sharper.",
            "Reduce filler words in the opening.",
            "Land the opening line with more intent."
        ]),

    ["structure"] = new AdviceTemplate(
        Label: "structure",
        Strength:
        [
            "Your answer follows a structure that is easy to track.",
            "Your ideas move in a clear sequence.",
            "You organize your points well under time pressure."
        ],
        Improve:
        [
            "Use a simple structure with two or three clear points.",
            "Signal your structure early (for example: 'two things come to mind').",
            "Keep each point focused before moving to the next.",
            "Use clearer transitions between ideas.",
            "Move forward through ideas instead of returning to the same one."
        ],
        Priority:
        [
            "Structure should be the main focus next round.",
            "Use a simple framework like point–example–takeaway.",
            "Try a clear pattern such as past–present–future or problem–solution."
        ],
        Polish:
        [
            "Make transitions between points smoother.",
            "Keep your points shorter so the structure stays visible.",
            "Tighten the flow from opening to conclusion."
        ]),

    ["ending"] = new AdviceTemplate(
        Label: "ending",
        Strength:
        [
            "You finish with a clear closing thought.",
            "Your answer ends with a sense of completion.",
            "You close the response in a controlled way."
        ],
        Improve:
        [
            "End with one concise takeaway.",
            "Signal the ending with a short summary sentence.",
            "Avoid introducing new ideas at the very end.",
            "Restate the core message briefly before finishing.",
            "Finish with a clear final sentence instead of fading out."
        ],
        Priority:
        [
            "Focus on ending your answer more clearly.",
            "Prioritize a deliberate final sentence.",
            "Make the closing idea simple and direct."
        ],
        Polish:
        [
            "Make the final sentence slightly shorter and sharper.",
            "Let the last idea land before stopping.",
            "Close by reinforcing the main message."
        ]),

    ["confidence"] = new AdviceTemplate(
        Label: "confidence",
        Strength:
        [
            "Your delivery sounds composed.",
            "You speak with steady control.",
            "Your tone projects confidence."
        ],
        Improve:
        [
            "Slow down slightly to sound more deliberate.",
            "Replace filler words with brief pauses.",
            "Keep your voice steady through key points.",
            "Pause briefly before important ideas.",
            "Finish sentences confidently instead of softening them."
        ],
        Priority:
        [
            "Confidence is the main area to strengthen next.",
            "Focus on a calmer pace and steadier delivery.",
            "Reduce hesitation and commit to each sentence."
        ],
        Polish:
        [
            "Add cleaner pauses between ideas.",
            "Keep the tone steady from start to finish.",
            "Deliver key sentences with more intent."
        ]),

    ["clarity"] = new AdviceTemplate(
        Label: "clarity",
        Strength:
        [
            "Your ideas are easy to follow.",
            "You explain your points clearly.",
            "Your message comes across directly."
        ],
        Improve:
        [
            "Use shorter sentences for key ideas.",
            "State the main point before explaining it.",
            "Keep one idea per sentence.",
            "Remove details that distract from the main point.",
            "Use concrete examples to clarify your idea."
        ],
        Priority:
        [
            "Clarity should be your main focus next round.",
            "Simplify sentences so the main idea stands out.",
            "Focus on expressing one idea clearly at a time."
        ],
        Polish:
        [
            "Tighten longer sentences slightly.",
            "Keep the strongest ideas and trim extra detail.",
            "Make key statements more concise."
        ]),

    ["authenticity"] = new AdviceTemplate(
        Label: "authenticity",
        Strength:
        [
            "Your delivery feels natural.",
            "Your voice sounds genuine.",
            "You communicate with a personal tone."
        ],
        Improve:
        [
            "Use phrasing that feels more natural to you.",
            "Let your perspective appear earlier in the answer.",
            "Avoid overly formal wording.",
            "Explain the idea as you would in conversation.",
            "Let your tone stay relaxed and direct."
        ],
        Priority:
        [
            "Focus on sounding more natural in your delivery.",
            "Bring your personal perspective into the answer sooner.",
            "Keep your tone conversational and direct."
        ],
        Polish:
        [
            "Add one concrete personal angle when relevant.",
            "Reduce formal phrasing slightly.",
            "Make your perspective clearer in key moments."
        ]),

    ["languageExpression"] = new AdviceTemplate(
        Label: "language expression",
        Strength:
        [
            "Your language keeps the answer engaging.",
            "Your phrasing adds energy to your message.",
            "Your wording supports your ideas well."
        ],
        Improve:
        [
            "Use more precise wording for key ideas.",
            "Vary sentence rhythm to keep the speech dynamic.",
            "Avoid repeating the same phrasing too often.",
            "Use simpler wording when ideas become dense.",
            "Connect ideas with clearer transitions."
        ],
        Priority:
        [
            "Focus on clearer and more varied phrasing.",
            "Prioritize precise wording for important ideas.",
            "Work on making key sentences more expressive."
        ],
        Polish:
        [
            "Refine wording so key phrases sound sharper.",
            "Keep language vivid while staying concise.",
            "Use slightly more variation in phrasing."
        ])
        };

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

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var deleted = await _sessionRepository.DeleteByIdAsync(sessionId, cancellationToken);
        if (!deleted)
        {
            return false;
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SessionDto MapToDto(Session session)
    {
        var advice = BuildAdvice(session);

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
            Transcript = session.Transcript,
            Advice = advice
        };
    }

    private static string? BuildAdvice(Session session)
    {
        if (session.Status != SessionStatus.Completed)
            return null;

        var rated = new List<RatedArea>();
        AddRatedArea(rated, "opening", session.RatingOpening);
        AddRatedArea(rated, "structure", session.RatingStructure);
        AddRatedArea(rated, "ending", session.RatingEnding);
        AddRatedArea(rated, "confidence", session.RatingConfidence);
        AddRatedArea(rated, "clarity", session.RatingClarity);
        AddRatedArea(rated, "authenticity", session.RatingAuthenticity);
        AddRatedArea(rated, "languageExpression", session.RatingLanguageExpression);

        if (rated.Count == 0)
            return "Great session. Next time, add self-ratings so I can give you more precise advice.";

        var weakestScore = rated.Min(x => x.Score);
        var average = rated.Average(x => x.Score);
        var strongestWeakestTie = rated.Max(x => x.Score) == weakestScore;
        var tone = ResolveTone(average);

        if (strongestWeakestTie)
        {
            var tieCandidates = rated
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToArray();

            var selected = tieCandidates[PickIndex(session.Id, "tie", tieCandidates.Length)];
            var tieMessage = PickTieMessage(selected, tone, session.Id);
            var tiePrefix = BuildAdvicePrefix(tone, selected.Template.Label);
            return $"{tiePrefix} {tieMessage}";
        }

        var weakestCandidates = rated
            .Where(x => x.Score == weakestScore)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        var weakest = weakestCandidates[PickIndex(session.Id, "weakest", weakestCandidates.Length)];
        var weakestText = PickWeakestMessage(weakest, weakestScore, session.Id);
        var weakestPrefix = BuildAdvicePrefix(tone, weakest.Template.Label);
        return $"{weakestPrefix} {weakestText}";
    }

    private static AdviceTone ResolveTone(double average)
    {
        if (average >= 4.2) return AdviceTone.Strong;
        if (average >= 3.2) return AdviceTone.Solid;
        return AdviceTone.ImprovementFocused;
    }

    private static string BuildAdvicePrefix(AdviceTone tone, string label) => tone switch
    {
        AdviceTone.Strong => $"Next polish: {label}.",
        AdviceTone.Solid => $"Focus next on {label}.",
        _ => $"Priority: {label}."
    };

    private static string PickTieMessage(RatedArea area, AdviceTone tone, Guid sessionId) => tone switch
    {
        AdviceTone.Strong => PickMessage(area.Template.Polish, sessionId, $"{area.Key}:polish"),
        AdviceTone.Solid => PickMessage(area.Template.Improve, sessionId, $"{area.Key}:improve"),
        _ => PickMessage(area.Template.Priority, sessionId, $"{area.Key}:priority")
    };

    private static string PickWeakestMessage(RatedArea weakest, int weakestScore, Guid sessionId) => weakestScore switch
    {
        <= 2 => PickMessage(weakest.Template.Priority, sessionId, $"{weakest.Key}:priority"),
        3 => PickMessage(weakest.Template.Improve, sessionId, $"{weakest.Key}:improve"),
        _ => PickMessage(weakest.Template.Polish, sessionId, $"{weakest.Key}:polish")
    };

    private static void AddRatedArea(List<RatedArea> rated, string key, int? score)
    {
        if (!score.HasValue) return;
        if (!AdviceTemplates.TryGetValue(key, out var template)) return;
        rated.Add(new RatedArea(key, score.Value, template));
    }

    private static string PickMessage(string[] options, Guid sessionId, string salt)
    {
        if (options.Length == 0) return string.Empty;
        return options[PickIndex(sessionId, salt, options.Length)];
    }

    private static int PickIndex(Guid sessionId, string salt, int size)
    {
        if (size <= 1) return 0;

        var input = sessionId.ToString("N") + ":" + salt;
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in input)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return (int)(hash % (uint)size);
        }
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

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
                    "You start with a clear first idea.",
                    "Your opening gets to the point quickly.",
                    "You begin in a way that gives the speech direction."
                ],
                Improve:
                [
                    "Start with a clearer first idea.",
                    "Use a simple framing sentence to begin.",
                    "State your angle earlier.",
                    "Avoid circling before making your point.",
                    "Open with a concrete thought instead of a vague intro."
                ],
                Priority:
                [
                    "Your opening needs to become more decisive.",
                    "Start faster and make the first sentence intentional.",
                    "Give the listener a clear entry into your answer."
                ],
                Polish:
                [
                    "Polish the first sentence to sound even more intentional.",
                    "Keep your opening crisp and reduce filler words.",
                    "Use a sharper first line to set the tone immediately."
                ]),
            ["structure"] = new AdviceTemplate(
                Label: "structure",
                Strength:
                [
                    "Your answer has a clear shape.",
                    "Your ideas follow a structure that is easy to track.",
                    "You organize your points well under time pressure."
                ],
                Improve:
                [
                    "Organize your answer into two or three points.",
                    "Use clearer transitions between ideas.",
                    "Keep each point focused.",
                    "Move forward instead of circling the same idea.",
                    "Think in short idea blocks."
                ],
                Priority:
                [
                    "Structure should be your main focus next round.",
                    "Give your answer a simple backbone: point, example, takeaway.",
                    "Reduce the number of ideas and organize them more clearly."
                ],
                Polish:
                [
                    "Polish transitions so each point connects smoothly.",
                    "Make your structure feel lighter by keeping points shorter.",
                    "Tighten the flow between opening, body, and ending."
                ]),
            ["ending"] = new AdviceTemplate(
                Label: "ending",
                Strength:
                [
                    "You close with a clear finish.",
                    "Your ending gives a good sense of completion.",
                    "You wrap up your answer in a controlled way."
                ],
                Improve:
                [
                    "End with one concise takeaway.",
                    "Reserve a final sentence to close intentionally.",
                    "Avoid stopping abruptly; land your final point.",
                    "Repeat your core message briefly at the end.",
                    "Finish with a stronger closing line."
                ],
                Priority:
                [
                    "Your ending needs a clearer final takeaway.",
                    "Prioritize a deliberate closing sentence.",
                    "Make your last line memorable and direct."
                ],
                Polish:
                [
                    "Polish your final line so it lands with more impact.",
                    "Make the ending shorter and more memorable.",
                    "Close with a cleaner callout of your main idea."
                ]),
            ["confidence"] = new AdviceTemplate(
                Label: "confidence",
                Strength:
                [
                    "You sound composed and assured.",
                    "Your delivery projects confidence.",
                    "You keep control of your pace and tone."
                ],
                Improve:
                [
                    "Slow down slightly to sound more in control.",
                    "Use fewer fillers to reinforce confidence.",
                    "Keep your voice steady through key points.",
                    "Pause briefly before important sentences.",
                    "Commit to your wording instead of softening statements."
                ],
                Priority:
                [
                    "Confidence is the main area to strengthen next.",
                    "Prioritize a calmer pace and stronger vocal intent.",
                    "Reduce hesitation and own each sentence."
                ],
                Polish:
                [
                    "Polish confidence by adding cleaner pauses.",
                    "Keep your tone steady from start to finish.",
                    "Sharpen delivery by removing soft qualifiers."
                ]),
            ["clarity"] = new AdviceTemplate(
                Label: "clarity",
                Strength:
                [
                    "Your message is easy to follow.",
                    "You explain ideas in a clear and direct way.",
                    "Your wording helps the listener understand quickly."
                ],
                Improve:
                [
                    "Use shorter sentences for key ideas.",
                    "Replace vague terms with concrete wording.",
                    "Keep one main idea per sentence.",
                    "Remove extra details that dilute the point.",
                    "State your point before giving examples."
                ],
                Priority:
                [
                    "Clarity should be your top priority next round.",
                    "Prioritize simple phrasing and shorter idea blocks.",
                    "Focus on one clear message at a time."
                ],
                Polish:
                [
                    "Polish clarity by tightening long sentences.",
                    "Keep your best points and trim secondary details.",
                    "Make wording sharper and more concrete."
                ]),
            ["authenticity"] = new AdviceTemplate(
                Label: "authenticity",
                Strength:
                [
                    "You sound natural and genuine.",
                    "Your speaking style feels authentic.",
                    "You communicate with a personal, credible tone."
                ],
                Improve:
                [
                    "Use more natural phrasing that sounds like you.",
                    "Add one personal angle to ground your point.",
                    "Avoid sounding overly scripted.",
                    "Let your own perspective come through earlier.",
                    "Keep your tone conversational and specific."
                ],
                Priority:
                [
                    "Authenticity needs focused work next round.",
                    "Prioritize sounding more personal and less generic.",
                    "Bring your own perspective into the answer sooner."
                ],
                Polish:
                [
                    "Polish authenticity by adding one concrete personal detail.",
                    "Keep your natural voice and reduce formal phrasing.",
                    "Make your perspective more explicit in key moments."
                ]),
            ["languageExpression"] = new AdviceTemplate(
                Label: "language expression",
                Strength:
                [
                    "Your language is expressive and engaging.",
                    "You choose words that add energy to your message.",
                    "Your phrasing gives your speech presence."
                ],
                Improve:
                [
                    "Use more precise vocabulary for key ideas.",
                    "Vary sentence rhythm to keep the speech dynamic.",
                    "Replace repeated words with sharper alternatives.",
                    "Use simpler phrasing when ideas get dense.",
                    "Balance expressive language with clarity."
                ],
                Priority:
                [
                    "Language expression should be a priority next round.",
                    "Prioritize precision and variety in wording.",
                    "Work on expressive but concise phrasing."
                ],
                Polish:
                [
                    "Polish expression with stronger word variety.",
                    "Keep language vivid while staying concise.",
                    "Refine key phrases to sound more intentional."
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        ]),

    ["passion"] = new AdviceTemplate(
        Label: "passion & novelty",
        Strength:
        [
            "Your enthusiasm lifts the entire delivery.",
            "Your energy makes the content compelling.",
            "Your passion comes through naturally."
        ],
        Improve:
        [
            "Let your personal interest show more in key moments.",
            "Add a surprising angle or fresh perspective.",
            "Show why this topic matters to you specifically.",
            "Bring more energy to the moments that excite you.",
            "Find one unexpected insight to share."
        ],
        Priority:
        [
            "Inject more genuine enthusiasm into delivery.",
            "Find what excites you about this topic and lead with it.",
            "Add a fresh angle the audience wouldn't expect."
        ],
        Polish:
        [
            "Let your passion peak at the most important point.",
            "Add one surprising detail or perspective.",
            "Channel your energy more precisely into key moments."
        ])
        };

    // === Interview Speech Advice Templates (STAR framework) ===
    private static readonly IReadOnlyDictionary<string, AdviceTemplate> InterviewAdviceTemplates =
        new Dictionary<string, AdviceTemplate>(StringComparer.OrdinalIgnoreCase)
    {
        ["relevance"] = new AdviceTemplate(
            Label: "relevance",
            Strength:
            [
                "Your example directly addresses the question asked.",
                "You chose a highly relevant story for this question.",
                "Your answer maps perfectly to what was being asked."
            ],
            Improve:
            [
                "Make sure the story you pick directly answers the specific question.",
                "Start by restating the core of the question to anchor your answer.",
                "Choose an example that demonstrates the exact skill being asked about.",
                "Ask yourself: does this story prove I have the skill they are testing?",
                "If your example feels tangential, pivot to a more targeted one."
            ],
            Priority:
            [
                "Relevance is critical — the wrong story undermines everything else.",
                "Before speaking, confirm your example actually answers the question.",
                "Pick a story that directly demonstrates the requested competency."
            ],
            Polish:
            [
                "Make the connection between question and story even more explicit.",
                "Open with a sentence that bridges the question to your example.",
                "Tighten the link so the interviewer never has to guess the relevance."
            ]),

        ["situationStakes"] = new AdviceTemplate(
            Label: "situation & stakes",
            Strength:
            [
                "You set the context quickly and clearly.",
                "The stakes were immediately obvious.",
                "Your setup created genuine tension."
            ],
            Improve:
            [
                "Describe the situation in two sentences max — who, what, why it mattered.",
                "Make the stakes explicit: what would have gone wrong without action?",
                "Add a specific detail that makes the situation feel real (team size, deadline, dollar amount).",
                "Skip background that does not raise the stakes.",
                "Frame the situation as a problem that needed solving."
            ],
            Priority:
            [
                "Spend less time on setup — get to the tension faster.",
                "The interviewer needs to feel urgency within your first few sentences.",
                "State the risk or consequence upfront to create stakes."
            ],
            Polish:
            [
                "Tighten the setup to one crisp sentence plus one stakes sentence.",
                "Add one concrete metric to make the stakes more vivid.",
                "Remove any context that does not raise tension."
            ]),

        ["action"] = new AdviceTemplate(
            Label: "personal action",
            Strength:
            [
                "You clearly described what YOU specifically did.",
                "Your actions showed initiative and ownership.",
                "The steps you took were concrete and well-explained."
            ],
            Improve:
            [
                "Use 'I' not 'we' — the interviewer wants YOUR contribution.",
                "Describe the specific steps you took, not what the team did.",
                "Explain WHY you chose that approach over alternatives.",
                "Break your action into clear sequential steps.",
                "Show decision-making: what tradeoffs did you consider?"
            ],
            Priority:
            [
                "Action is the most important part — this is where you prove your value.",
                "Replace every 'we' with 'I' and describe YOUR specific contribution.",
                "Show the interviewer exactly what you did differently that made an impact."
            ],
            Polish:
            [
                "Add one sentence about why you chose this approach over others.",
                "Make the sequence of actions crisper.",
                "Highlight the moment of highest personal ownership."
            ]),

        ["resultImpact"] = new AdviceTemplate(
            Label: "result & impact",
            Strength:
            [
                "You quantified the outcome convincingly.",
                "The result clearly demonstrated your impact.",
                "Your metrics made the achievement tangible."
            ],
            Improve:
            [
                "End with a specific metric: percentage, dollar amount, or time saved.",
                "If you cannot quantify, describe the observable change that resulted.",
                "Connect the result back to the original problem.",
                "Mention what you learned or would do differently.",
                "State the lasting impact: did this become a standard practice?"
            ],
            Priority:
            [
                "Every STAR answer must end with a measurable result.",
                "Without numbers, your story is just an anecdote — add a metric.",
                "The result is what proves the action was worth taking."
            ],
            Polish:
            [
                "Make the metric more specific (exact percentage, timeline).",
                "Add a brief note about what you learned.",
                "Connect the result to broader business impact."
            ]),

        ["deliveryComposure"] = new AdviceTemplate(
            Label: "delivery & composure",
            Strength:
            [
                "You delivered the answer with composure and confidence.",
                "Your pacing felt natural and controlled.",
                "You maintained presence throughout the answer."
            ],
            Improve:
            [
                "Slow down during the action and result sections.",
                "Pause briefly between situation, action, and result.",
                "Reduce filler words — replace them with short pauses.",
                "Maintain eye contact (or camera focus) during key moments.",
                "Keep your energy consistent from start to finish."
            ],
            Priority:
            [
                "Focus on a calmer, more deliberate delivery pace.",
                "Replace hesitation with confident pauses.",
                "Practice the answer out loud to build composure."
            ],
            Polish:
            [
                "Add cleaner pauses at transition points.",
                "Keep vocal energy steady through the result section.",
                "End the answer with a firm, confident final sentence."
            ]),

        ["conciseness"] = new AdviceTemplate(
            Label: "conciseness",
            Strength:
            [
                "Your answer was tight and well-paced.",
                "You covered everything without rambling.",
                "Every sentence earned its place."
            ],
            Improve:
            [
                "Aim for 60 to 90 seconds total — you went longer.",
                "Cut background that does not raise stakes.",
                "Remove tangential details from the action section.",
                "One example is enough — do not stack multiple stories.",
                "End after stating the result — do not add unnecessary caveats."
            ],
            Priority:
            [
                "Shorter answers score higher — trim ruthlessly.",
                "The interviewer remembers structure, not length.",
                "Practice delivering the same story in half the time."
            ],
            Polish:
            [
                "Tighten by removing one sentence from each STAR section.",
                "Cut the weakest detail from the situation setup.",
                "End one sentence earlier than feels natural."
            ])
    };

    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServiceScopeFactory _scopeFactory;

    public SessionService(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IServiceScopeFactory scopeFactory)
    {
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _scopeFactory = scopeFactory;
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
        var sessionType = string.IsNullOrWhiteSpace(request.Type)
            ? SessionType.General
            : ParseEnum<SessionType>(request.Type);

        var session = Session.Create(
            request.Id,
            userId,
            request.CreatedAt,
            mode,
            language,
            difficulty,
            request.Word,
            request.ThinkSeconds,
            request.SpeakSeconds,
            sessionType);

        // Set status
        session.SetStatus(status, cancelReason);

        // Set completed at if provided
        if (request.CompletedAt.HasValue)
        {
            session.SetCompletedAt(request.CompletedAt.Value);
        }

        // Set ratings based on session type
        if (sessionType == SessionType.Interview && request.InterviewRatings != null)
        {
            session.SetInterviewRatings(
                request.InterviewRatings.Relevance,
                request.InterviewRatings.SituationStakes,
                request.InterviewRatings.Action,
                request.InterviewRatings.ResultImpact,
                request.InterviewRatings.DeliveryComposure,
                request.InterviewRatings.Conciseness);
        }
        else if (request.Ratings != null)
        {
            session.SetGeneralRatings(
                request.Ratings.Opening,
                request.Ratings.Structure,
                request.Ratings.Ending,
                request.Ratings.Confidence,
                request.Ratings.Clarity,
                request.Ratings.Authenticity,
                request.Ratings.LanguageExpression,
                request.Ratings.Passion);
        }

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
                audioError,
                request.Audio.ObjectKey,
                request.Audio.BucketName,
                request.Audio.Region,
                request.Audio.UploadedAt);
        }

        // Set transcript (from frontend, if provided)
        session.SetTranscript(request.Transcript);

        // Save to database
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // Trigger server-side transcription if audio is available and no client transcript was provided
        if (session.AudioAvailable
            && !string.IsNullOrWhiteSpace(session.AudioObjectKey)
            && string.IsNullOrWhiteSpace(request.Transcript))
        {
            // Determine language code
            string? langCode = null;
            if (session.Type == SessionType.Interview)
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                langCode = user?.ResumeLanguage;
            }
            else
            {
                langCode = session.Language switch
                {
                    SessionLanguage.En => "en",
                    SessionLanguage.Fr => "fr",
                    SessionLanguage.Ar => "ar",
                    _ => null
                };
            }

            session.SetTranscriptionStatus("Pending");
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);

            var audioKey = session.AudioObjectKey;
            var sid = session.Id;
            var lang = langCode;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var transcriptionService = scope.ServiceProvider.GetRequiredService<ITranscriptionService>();
                    await transcriptionService.TranscribeSessionAsync(sid, audioKey, lang);
                }
                catch (Exception ex)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionService>>();
                    logger.LogError(ex, "Background transcription failed for session {SessionId}.", sid);
                }
            });
        }

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

    public async Task SetSpeechAnalysisAsync(Guid sessionId, string analysisJson, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session == null) return;

        session.SetSpeechAnalysis(analysisJson);
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);
    }

    private static SessionDto MapToDto(Session session)
    {
        var advice = BuildAdvice(session);

        GeneralRatingsDto? generalRatings = null;
        InterviewRatingsDto? interviewRatings = null;

        if (session.Type == SessionType.Interview)
        {
            interviewRatings = new InterviewRatingsDto
            {
                Relevance = session.RatingRelevance,
                SituationStakes = session.RatingSituationStakes,
                Action = session.RatingAction,
                ResultImpact = session.RatingResultImpact,
                DeliveryComposure = session.RatingDeliveryComposure,
                Conciseness = session.RatingConciseness
            };
        }
        else
        {
            generalRatings = new GeneralRatingsDto
            {
                Opening = session.RatingOpening,
                Structure = session.RatingStructure,
                Ending = session.RatingEnding,
                Confidence = session.RatingConfidence,
                Clarity = session.RatingClarity,
                Authenticity = session.RatingAuthenticity,
                LanguageExpression = session.RatingLanguageExpression,
                Passion = session.RatingPassion
            };
        }

        return new SessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            CreatedAt = session.CreatedAt,
            CompletedAt = session.CompletedAt,
            Mode = session.Mode.ToString(),
            Type = session.Type.ToString(),
            Language = session.Language.ToString().ToUpperInvariant(),
            Difficulty = session.Difficulty.ToString().ToUpperInvariant(),
            Word = session.Word,
            ThinkSeconds = session.ThinkSeconds,
            SpeakSeconds = session.SpeakSeconds,
            Status = session.Status.ToString(),
            CancelReason = session.CancelReason?.ToString(),
            Ratings = generalRatings,
            InterviewRatings = interviewRatings,
            ManualScore = CalculateManualScore(session),
            AiScore = CalculateAiScore(session),
            Notes = session.Notes,
            Audio = new SessionAudioDto
            {
                Available = session.AudioAvailable,
                DurationMs = session.AudioDurationMs,
                RecordingStartedAt = session.AudioRecordingStartedAt,
                RecordingEndedAt = session.AudioRecordingEndedAt,
                ErrorCode = session.AudioErrorCode?.ToString(),
                ObjectKey = session.AudioObjectKey,
                BucketName = session.AudioBucketName,
                Region = session.AudioRegion,
                UploadedAt = session.AudioUploadedAt
            },
            Transcript = session.Transcript,
            TranscriptionStatus = session.TranscriptionStatus,
            TranscriptionError = session.TranscriptionError,
            Advice = advice,
            SpeechAnalysis = string.IsNullOrWhiteSpace(session.SpeechAnalysis)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<object>(session.SpeechAnalysis),
            AnalyzedAt = session.AnalyzedAt,
            AiScored = session.AiScored
        };
    }

    private static string? BuildAdvice(Session session)
    {
        if (session.Status != SessionStatus.Completed)
            return null;

        var rated = new List<RatedArea>();

        if (session.Type == SessionType.Interview)
        {
            AddRatedArea(rated, "relevance", session.RatingRelevance, InterviewAdviceTemplates);
            AddRatedArea(rated, "situationStakes", session.RatingSituationStakes, InterviewAdviceTemplates);
            AddRatedArea(rated, "action", session.RatingAction, InterviewAdviceTemplates);
            AddRatedArea(rated, "resultImpact", session.RatingResultImpact, InterviewAdviceTemplates);
            AddRatedArea(rated, "deliveryComposure", session.RatingDeliveryComposure, InterviewAdviceTemplates);
            AddRatedArea(rated, "conciseness", session.RatingConciseness, InterviewAdviceTemplates);
        }
        else
        {
            AddRatedArea(rated, "opening", session.RatingOpening);
            AddRatedArea(rated, "structure", session.RatingStructure);
            AddRatedArea(rated, "ending", session.RatingEnding);
            AddRatedArea(rated, "confidence", session.RatingConfidence);
            AddRatedArea(rated, "clarity", session.RatingClarity);
            AddRatedArea(rated, "authenticity", session.RatingAuthenticity);
            AddRatedArea(rated, "languageExpression", session.RatingLanguageExpression);
            AddRatedArea(rated, "passion", session.RatingPassion);
        }

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

    private static void AddRatedArea(List<RatedArea> rated, string key, int? score,
        IReadOnlyDictionary<string, AdviceTemplate>? templates = null)
    {
        if (!score.HasValue) return;
        templates ??= AdviceTemplates;
        if (!templates.TryGetValue(key, out var template)) return;
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

    /// <summary>
    /// Calculate manual score from self-rated criteria. Returns null if no ratings.
    /// General: weighted sum * 2 on 0-10 scale + passion bonus (max 10.5).
    /// Interview: weighted sum * 2 on 0-10 scale with gating (max 10).
    /// </summary>
    private static decimal? CalculateManualScore(Session session)
    {
        if (session.Type == SessionType.Interview)
        {
            if (!session.RatingRelevance.HasValue || !session.RatingSituationStakes.HasValue ||
                !session.RatingAction.HasValue || !session.RatingResultImpact.HasValue ||
                !session.RatingDeliveryComposure.HasValue || !session.RatingConciseness.HasValue)
                return null;

            var weighted =
                session.RatingRelevance.Value * 0.20m +
                session.RatingSituationStakes.Value * 0.15m +
                session.RatingAction.Value * 0.30m +
                session.RatingResultImpact.Value * 0.20m +
                session.RatingDeliveryComposure.Value * 0.10m +
                session.RatingConciseness.Value * 0.05m;

            var score = weighted * 2m;
            if (session.RatingRelevance.Value == 1)
                score = Math.Min(score, 4.0m);

            return Math.Round(score, 1);
        }
        else
        {
            if (!session.RatingOpening.HasValue || !session.RatingStructure.HasValue ||
                !session.RatingEnding.HasValue || !session.RatingConfidence.HasValue ||
                !session.RatingClarity.HasValue || !session.RatingAuthenticity.HasValue ||
                !session.RatingLanguageExpression.HasValue)
                return null;

            var weighted =
                session.RatingOpening.Value * 0.15m +
                session.RatingStructure.Value * 0.20m +
                session.RatingEnding.Value * 0.15m +
                session.RatingConfidence.Value * 0.15m +
                session.RatingClarity.Value * 0.15m +
                session.RatingAuthenticity.Value * 0.10m +
                session.RatingLanguageExpression.Value * 0.10m;

            var score = weighted * 2m;
            if (session.RatingPassion.HasValue)
                score += session.RatingPassion.Value * 0.05m * 2m;

            return Math.Min(Math.Round(score, 1), 10.5m);
        }
    }

    /// <summary>
    /// Extract AI score from stored SpeechAnalysis JSON. Returns null if no analysis.
    /// Scales the AI total_score (out of 105 for general, 100 for interview) to 0-10.
    /// </summary>
    private static decimal? CalculateAiScore(Session session)
    {
        if (string.IsNullOrWhiteSpace(session.SpeechAnalysis))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(session.SpeechAnalysis);
            if (doc.RootElement.TryGetProperty("total_score", out var totalScoreElement))
            {
                var totalScore = totalScoreElement.GetDouble();
                if (session.Type == SessionType.Interview)
                    return Math.Round((decimal)totalScore / 10m, 1);
                else
                    return Math.Round((decimal)totalScore / 10.5m, 1);
            }
        }
        catch
        {
            // SpeechAnalysis JSON might not have total_score yet
        }

        return null;
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

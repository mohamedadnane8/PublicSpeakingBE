using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Configuration;

namespace MyApp.Infrastructure.Services;

public class SpeechAnalysisService : ISpeechAnalysisService
{
    private const int MinWordCountForAi = 30;

    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<SpeechAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SpeechAnalysisService(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<SpeechAnalysisService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GeneralSpeechAnalysisDto> AnalyzeGeneralSpeechAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        if (IsTooShortForAnalysis(transcript))
            return BuildMinScoreGeneralAnalysis(transcript);

        var prompt = BuildGeneralPrompt(transcript);
        var content = await CallDeepSeekAsync(prompt, cancellationToken);

        var raw = JsonSerializer.Deserialize<RawGeneralAnalysisDto>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize general speech analysis.");

        return ComputeGeneralScores(raw);
    }

    public async Task<InterviewSpeechAnalysisDto> AnalyzeInterviewSpeechAsync(
        string question,
        string transcript,
        CancellationToken cancellationToken = default)
    {
        if (IsTooShortForAnalysis(transcript))
            return BuildMinScoreInterviewAnalysis(transcript);

        var prompt = BuildInterviewPrompt(question, transcript);
        var content = await CallDeepSeekAsync(prompt, cancellationToken);

        var raw = JsonSerializer.Deserialize<RawInterviewAnalysisDto>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize interview speech analysis.");

        return ComputeInterviewScores(raw);
    }

    // === Short transcript bypass — all 1s, no AI call ===

    private static bool IsTooShortForAnalysis(string transcript)
    {
        var wordCount = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return wordCount < MinWordCountForAi;
    }

    private static GeneralSpeechAnalysisDto BuildMinScoreGeneralAnalysis(string transcript)
    {
        var wordCount = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var duration = Math.Round(wordCount / 130.0, 1);
        const string feedback = "Transcript too short for meaningful analysis.";

        var raw1 = new RawCriterionScoreDto { Raw = 1, Feedback = feedback };
        var emptyFiller = BuildEmptyFillerAnalysis(wordCount, duration);

        return ComputeGeneralScores(new RawGeneralAnalysisDto
        {
            FillerAnalysis = emptyFiller,
            Scores = new RawGeneralScoresDto
            {
                Opening = raw1, Structure = raw1, Closing = raw1, Confidence = raw1,
                Clarity = raw1, Authenticity = raw1, Language = raw1, Passion = raw1
            },
            TopStrength = "N/A — transcript too short.",
            TopImprovement = "Provide a longer response to enable meaningful evaluation."
        });
    }

    private static InterviewSpeechAnalysisDto BuildMinScoreInterviewAnalysis(string transcript)
    {
        var wordCount = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var duration = Math.Round(wordCount / 130.0, 1);
        const string feedback = "Transcript too short for meaningful analysis.";

        var raw1 = new RawCriterionScoreDto { Raw = 1, Feedback = feedback };
        var emptyFiller = BuildEmptyFillerAnalysis(wordCount, duration);

        return ComputeInterviewScores(new RawInterviewAnalysisDto
        {
            FillerAnalysis = emptyFiller,
            Scores = new RawInterviewScoresDto
            {
                Relevance = raw1, Situation = raw1, Action = raw1,
                Result = raw1, Delivery = raw1, Conciseness = raw1
            },
            TopStrength = "N/A — transcript too short.",
            TopImprovement = "Provide a longer response to enable meaningful evaluation."
        });
    }

    private static FillerAnalysisDto BuildEmptyFillerAnalysis(int wordCount, double duration) => new()
    {
        TotalCount = 0,
        EstimatedDurationMinutes = duration,
        FillerRatePerMinute = 0,
        RateLabel = "N/A",
        Breakdown = new FillerBreakdownDto
        {
            HesitationSounds = new FillerCategoryDto { Count = 0, Words = new() },
            PaddingPhrases = new FillerCategoryDto { Count = 0, Words = new() },
            VerbalTics = new FillerCategoryDto { Count = 0, Words = new() },
            Restarts = new RestartsDto { Count = 0 }
        },
        TopOffenders = []
    };

    // === Server-side score calculation ===

    private static GeneralSpeechAnalysisDto ComputeGeneralScores(RawGeneralAnalysisDto raw)
    {
        var s = raw.Scores;

        var openingW = s.Opening.Raw / 5.0 * 15;
        var structureW = s.Structure.Raw / 5.0 * 20;
        var closingW = s.Closing.Raw / 5.0 * 15;
        var confidenceW = s.Confidence.Raw / 5.0 * 15;
        var clarityW = s.Clarity.Raw / 5.0 * 15;
        var authenticityW = s.Authenticity.Raw / 5.0 * 10;
        var languageW = s.Language.Raw / 5.0 * 10;
        var passionBonus = (double)s.Passion.Raw; // raw 1-5 added directly

        var baseScore = Math.Round(openingW + structureW + closingW + confidenceW + clarityW + authenticityW + languageW, 1);
        var totalScore = Math.Round(baseScore + passionBonus, 1);

        return new GeneralSpeechAnalysisDto
        {
            FillerAnalysis = raw.FillerAnalysis,
            Scores = new GeneralScoresDto
            {
                Opening = ToCriterion(s.Opening, openingW),
                Structure = ToCriterion(s.Structure, structureW),
                Closing = ToCriterion(s.Closing, closingW),
                Confidence = ToCriterion(s.Confidence, confidenceW),
                Clarity = ToCriterion(s.Clarity, clarityW),
                Authenticity = ToCriterion(s.Authenticity, authenticityW),
                Language = ToCriterion(s.Language, languageW),
                Passion = new PassionScoreDto
                {
                    Raw = s.Passion.Raw,
                    BonusPoints = passionBonus,
                    Feedback = s.Passion.Feedback
                }
            },
            BaseScore = baseScore,
            TotalScore = totalScore,
            Band = GetBand(totalScore),
            TopStrength = raw.TopStrength,
            TopImprovement = raw.TopImprovement
        };
    }

    private static InterviewSpeechAnalysisDto ComputeInterviewScores(RawInterviewAnalysisDto raw)
    {
        var s = raw.Scores;

        var relevanceW = s.Relevance.Raw / 5.0 * 20;
        var situationW = s.Situation.Raw / 5.0 * 15;
        var actionW = s.Action.Raw / 5.0 * 30;
        var resultW = s.Result.Raw / 5.0 * 20;
        var deliveryW = s.Delivery.Raw / 5.0 * 10;
        var concisenessW = s.Conciseness.Raw / 5.0 * 5;

        var baseScore = Math.Round(relevanceW + situationW + actionW + resultW + deliveryW + concisenessW, 1);
        var gateApplied = s.Relevance.Raw == 1;
        var totalScore = gateApplied ? Math.Min(baseScore, 40) : baseScore;

        return new InterviewSpeechAnalysisDto
        {
            FillerAnalysis = raw.FillerAnalysis,
            Scores = new InterviewScoresDto
            {
                Relevance = ToCriterion(s.Relevance, relevanceW),
                Situation = ToCriterion(s.Situation, situationW),
                Action = ToCriterion(s.Action, actionW),
                Result = ToCriterion(s.Result, resultW),
                Delivery = ToCriterion(s.Delivery, deliveryW),
                Conciseness = ToCriterion(s.Conciseness, concisenessW)
            },
            GateApplied = gateApplied,
            BaseScore = baseScore,
            TotalScore = totalScore,
            Band = GetBand(totalScore),
            TopStrength = raw.TopStrength,
            TopImprovement = raw.TopImprovement
        };
    }

    private static CriterionScoreDto ToCriterion(RawCriterionScoreDto raw, double weighted) => new()
    {
        Raw = raw.Raw,
        Weighted = Math.Round(weighted, 1),
        Feedback = raw.Feedback
    };

    private static string GetBand(double totalScore) => totalScore switch
    {
        >= 85 => "Exceptional",
        >= 65 => "Solid",
        >= 45 => "Developing",
        _ => "Needs Work"
    };

    // === Scoring scale conversion (for AI score → 0-10 scale used by ManualScore) ===

    public static decimal? ComputeAiScoreOn10Scale(GeneralSpeechAnalysisDto analysis)
    {
        // totalScore is out of 105 (100 base + 5 bonus). Scale to 0-10.
        return Math.Round((decimal)analysis.TotalScore / 10.5m, 1);
    }

    public static decimal? ComputeAiScoreOn10Scale(InterviewSpeechAnalysisDto analysis)
    {
        // totalScore is out of 100. Scale to 0-10.
        return Math.Round((decimal)analysis.TotalScore / 10m, 1);
    }

    // === DeepSeek API call ===

    private async Task<string> CallDeepSeekAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 4096,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        _logger.LogInformation("Calling DeepSeek for speech analysis (prompt length={Length})", prompt.Length);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("DeepSeek speech analysis returned {StatusCode} after {ElapsedMs}ms: {Body}",
                response.StatusCode, sw.ElapsedMilliseconds, errorBody);
            response.EnsureSuccessStatusCode();
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var chatResponse = await JsonSerializer.DeserializeAsync<ChatResponse>(responseStream, JsonOptions, cancellationToken);
        sw.Stop();

        var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("DeepSeek returned empty content for speech analysis.");

        _logger.LogInformation("DeepSeek speech analysis completed in {ElapsedMs}ms (response length={Length})",
            sw.ElapsedMilliseconds, content.Length);

        return content;
    }

    // === Prompts — AI returns raw scores only, no math ===

    private static string BuildGeneralPrompt(string transcript)
    {
        return $$$"""
You are an expert public speaking coach. Evaluate this speech transcript. Return ONLY raw scores (1-5) and feedback per criterion. Do NOT calculate weighted scores, totals, or bands — the server handles that.

CALIBRATION: A truly average speech should score around 55-60 total (when weighted). A score of 80+ should be rare. Do not give a 5 on any criterion unless the behavior is genuinely exceptional — most speeches should have no 5s. Be honest and constructive, not generous.
---
TRANSCRIPT:
{{{transcript}}}
---
STEP 1 — LANGUAGE DETECTION & FILLER WORD ANALYSIS (do this first)
Detect the transcript language first. Then count filler words using the appropriate list below.

ENGLISH filler words:
Hesitation sounds: uh, um, er, ah, eh, hmm
Padding phrases: you know, I mean, like (filler), right (filler), okay (filler), so (sentence starter), basically, literally (filler), actually (filler), honestly, obviously, clearly (filler)
Verbal tics: kind of, sort of, a little bit, at the end of the day, to be honest, to be fair, in terms of, the thing is, like I said, you know what I mean

FRENCH filler words:
Hesitation sounds: euh, heu, ah, hm, ben, bah
Padding phrases: genre (filler), en fait, du coup, tu vois, vous voyez, c'est-à-dire, quoi (end of sentence filler), bon (filler), voilà (filler), bref, enfin (filler), disons
Verbal tics: un peu, en gros, comment dire, je veux dire, tu sais, vous savez, c'est vrai que, après (filler connector), au final, par contre (overused)

ARABIC filler words:
Hesitation sounds: آه (aah), إيه (eeh), أم (umm), هم (hmm), يعني (ya'ni - filler use)
Padding phrases: والله (wallah - filler), طبعاً (tab'an - filler), بالضبط (biddabt - filler), هيك (heik), يعني هيك (ya'ni heik), شو اسمو (shu ismo), كيف بدي قول (kif baddi 2ul)
Verbal tics: مش هيك (mish heik), الله يخليك (allah ykhalliq - filler), بصراحة (bisaraha), الحقيقة (al-haqi'a), في الآخر (fil-akhir)

Restarts: Count any instance where the speaker starts then restarts mid-sentence (in any language).
Filler rate = total fillers / (word count / 130). Labels: Excellent < 2/min, Good 2–5, Developing 6–10, Needs Work > 10.

Write all feedback in the SAME language as the transcript.
---
STEP 2 — Score each criterion 1-5 independently:
SCORE ANCHORS: 1 = Missing/counterproductive. 2 = Weak. 3 = Developing. 4 = Strong. 5 = Exceptional.
Each feedback field must be 2-3 sentences. Be specific to THIS transcript — quote or reference actual phrases the speaker used.

1. OPENING HOOK: Score 1-2 if generic opener or apology. 3 if attempted but weak. 4-5 if hooks in first 10 seconds (counterintuitive statement, mini story, surprising fact, provocative question, bold declaration).

2. NARRATIVE STRUCTURE: Score 1-2 if unorganized stream. 3 if rough arc but weak middle. 4-5 if clear three-part arc with tension/contrast.

3. CLOSING: Score 1-2 if fades out or "yeah, that's it". 3 if generic signal. 4-5 if intentional callback, one-liner, call to action, or emotional peak.

4. CONFIDENCE & PRESENCE: USE FILLER ANALYSIS. 1-2 if rate > 10/min. 3 if 6-10/min. 4 if 2-5/min. 5 if < 2/min with intentional pauses. Feedback MUST state: filler count, duration, rate, top offenders.

5. CLARITY: Score 1-2 if main point buried. 3 if eventually clear. 4-5 if crisp, main point in first 30 seconds.

6. AUTHENTICITY & WARMTH: Score 1-2 if scripted/detached. 3 if token personal reference. 4-5 if genuine connection, personal example.

7. LANGUAGE & EXPRESSION: Score 1-2 if abstract, no metaphor. 3 if one example. 4-5 if strong metaphor/analogy, varied rhythm.

8. PASSION & NOVELTY (bonus): Score 1-2 if flat. 3 if some enthusiasm. 4-5 if passion audible, novel insight.
---
OUTPUT — return ONLY valid JSON matching this structure:
{
  "filler_analysis": {
    "total_count": 0,
    "estimated_duration_minutes": 0.0,
    "filler_rate_per_minute": 0.0,
    "rate_label": "",
    "breakdown": {
      "hesitation_sounds": { "count": 0, "words": {} },
      "padding_phrases": { "count": 0, "words": {} },
      "verbal_tics": { "count": 0, "words": {} },
      "restarts": { "count": 0 }
    },
    "top_offenders": []
  },
  "scores": {
    "opening": { "raw": 0, "feedback": "" },
    "structure": { "raw": 0, "feedback": "" },
    "closing": { "raw": 0, "feedback": "" },
    "confidence": { "raw": 0, "feedback": "" },
    "clarity": { "raw": 0, "feedback": "" },
    "authenticity": { "raw": 0, "feedback": "" },
    "language": { "raw": 0, "feedback": "" },
    "passion": { "raw": 0, "feedback": "" }
  },
  "top_strength": "",
  "top_improvement": ""
}
""";
    }

    private static string BuildInterviewPrompt(string question, string transcript)
    {
        return $$$"""
You are a senior interview coach. Evaluate this interview answer. Return ONLY raw scores (1-5) and feedback per criterion. Do NOT calculate weighted scores, totals, or bands — the server handles that.

CALIBRATION: A truly average answer should score around 55-60 total (when weighted). A score of 80+ should be rare. Do not give a 5 on any criterion unless the behavior is genuinely exceptional — most answers should have no 5s. Be honest and constructive, not generous.

QUESTION-TYPE AWARENESS: If the question is behavioral (asks for a specific past example, e.g. "Tell me about a time..."), apply STAR framework strictly. If the question is NOT behavioral (e.g. "What's your greatest strength?", "Where do you see yourself in 5 years?", "Why this company?"), adapt: score Relevance on whether the answer addresses the question, score Action on the depth of self-analysis and reasoning, and score Result on whether concrete evidence supports the claims.
---
QUESTION ASKED:
{{{question}}}
TRANSCRIPT:
{{{transcript}}}
---
STEP 1 — LANGUAGE DETECTION & FILLER WORD ANALYSIS (do this first)
Detect the transcript language first. Then count filler words using the appropriate list below.

ENGLISH filler words:
Hesitation sounds: uh, um, er, ah, eh, hmm
Padding phrases: you know, I mean, like (filler), right (filler), okay (filler), so (sentence starter), basically, literally (filler), actually (filler), honestly, obviously, clearly (filler)
Verbal tics: kind of, sort of, a little bit, at the end of the day, to be honest, to be fair, in terms of, the thing is, like I said, you know what I mean

FRENCH filler words:
Hesitation sounds: euh, heu, ah, hm, ben, bah
Padding phrases: genre (filler), en fait, du coup, tu vois, vous voyez, c'est-à-dire, quoi (end of sentence filler), bon (filler), voilà (filler), bref, enfin (filler), disons
Verbal tics: un peu, en gros, comment dire, je veux dire, tu sais, vous savez, c'est vrai que, après (filler connector), au final, par contre (overused)

ARABIC filler words:
Hesitation sounds: آه (aah), إيه (eeh), أم (umm), هم (hmm), يعني (ya'ni - filler use)
Padding phrases: والله (wallah - filler), طبعاً (tab'an - filler), بالضبط (biddabt - filler), هيك (heik), يعني هيك (ya'ni heik), شو اسمو (shu ismo), كيف بدي قول (kif baddi 2ul)
Verbal tics: مش هيك (mish heik), الله يخليك (allah ykhalliq - filler), بصراحة (bisaraha), الحقيقة (al-haqi'a), في الآخر (fil-akhir)

Restarts: Count any instance where the speaker starts then restarts mid-sentence (in any language).
Filler rate = total fillers / (word count / 130). Labels: Excellent < 2/min, Good 2–5, Developing 6–10, Needs Work > 10.
Note where fillers cluster: start (nervousness), action section (uncertainty), result (lack of conviction), or throughout.

Write all feedback in the SAME language as the transcript.
---
STEP 2 — Score each criterion 1-5 independently:
SCORE ANCHORS: 1 = Missing/counterproductive. 2 = Weak. 3 = Developing. 4 = Strong. 5 = Exceptional.
Each feedback field must be 2-3 sentences. Be specific to THIS transcript — quote or reference actual phrases the speaker used.

A. RELEVANCE (GATING): Score 1 if wrong competency. 2 if relevant only in last 20%. 3 if mostly relevant with tangents. 4-5 if relevance established in first 15 seconds, competency is central.

B. SITUATION & STAKES: Score 1-2 if no context or > 40% of answer. 3 if basic but vague. 4-5 if crisp 2-3 sentence setup, real stakes, specific event.

C. PERSONAL ACTION (MOST IMPORTANT): Score 1-2 if "we" language, vague. 3 if some "I" but gaps. 4-5 if consistent "I", specific sequential steps, decision logic explained, ~50-60% of answer. KEY TEST: can you picture exactly what they did?

D. RESULT & IMPACT: Score 1-2 if absent/vague ("it went well"). 3 if qualitative. 4-5 if specific measurable outcome caused by speaker's actions.

E. DELIVERY & COMPOSURE: USE FILLER ANALYSIS. 1-2 if rate > 10/min. 3 if 6-10/min. 4 if 2-5/min. 5 if < 2/min. Feedback MUST state: filler count, duration, rate, top offenders, clustering.

F. CONCISENESS: Score 1-2 if > 3 minutes or repeats. 3 if slightly padded. 4-5 if complete at 90-150 seconds, every sentence moves forward.
---
OUTPUT — return ONLY valid JSON matching this structure:
{
  "filler_analysis": {
    "total_count": 0,
    "estimated_duration_minutes": 0.0,
    "filler_rate_per_minute": 0.0,
    "rate_label": "",
    "clustering_note": "",
    "breakdown": {
      "hesitation_sounds": { "count": 0, "words": {} },
      "padding_phrases": { "count": 0, "words": {} },
      "verbal_tics": { "count": 0, "words": {} },
      "restarts": { "count": 0 }
    },
    "top_offenders": []
  },
  "scores": {
    "relevance": { "raw": 0, "feedback": "" },
    "situation": { "raw": 0, "feedback": "" },
    "action": { "raw": 0, "feedback": "" },
    "result": { "raw": 0, "feedback": "" },
    "delivery": { "raw": 0, "feedback": "" },
    "conciseness": { "raw": 0, "feedback": "" }
  },
  "top_strength": "",
  "top_improvement": ""
}
""";
    }

    private class ChatResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        public Message? Message { get; set; }
    }

    private class Message
    {
        public string? Content { get; set; }
    }
}

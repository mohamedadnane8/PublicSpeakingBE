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

public class DeepSeekService : IDeepSeekService
{
    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeepSeekService(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeepSeekResponseDto> GenerateInterviewQuestionsAsync(
        string resumeText,
        int batchNumber,
        int questionsPerBatch,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(resumeText, batchNumber, questionsPerBatch);

        var requestBody = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "system", content = "You are an expert interviewer and career coach. You analyze resumes and generate tailored interview questions." },
                new { role = "user", content = prompt }
            },
            temperature = 0.5 + (batchNumber * 0.05),
            max_tokens = 8192,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("DeepSeek API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var chatResponse = await JsonSerializer.DeserializeAsync<DeepSeekChatResponse>(responseStream, JsonOptions, cancellationToken);

        var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("DeepSeek returned empty content for batch {Batch}.", batchNumber);
            return new DeepSeekResponseDto([], "en", "Unknown");
        }

        var parsed = JsonSerializer.Deserialize<DeepSeekResponseDto>(content, JsonOptions);
        return parsed ?? new DeepSeekResponseDto([], "en", "Unknown");
    }

    private static string BuildPrompt(string resumeText, int batchNumber, int count)
    {
        var batchInstruction = batchNumber switch
        {
            1 => $"Generate exactly {count} interview questions (batch 1: focus on technical skills, domain knowledge, and problem solving).",
            2 => $"Generate exactly {count} NEW and DIFFERENT interview questions (batch 2: focus on situational, leadership, and communication). Do NOT repeat questions from a typical first batch.",
            _ => $"Generate exactly {count} interview questions."
        };

        return $$"""
            You are a senior hiring manager conducting a real interview. Analyze this resume and generate {{count}} interview questions.

            {{batchInstruction}}

            CRITICAL RULES:
            1. Detect the resume language and write ALL questions in that SAME language.
            2. DO NOT generate generic behavioral questions (e.g., "Tell me about a time...") — those are handled separately.
            3. Every question MUST reference specific details from the resume: company names, technologies, projects, metrics, or job titles mentioned.
            4. Questions should sound like a real interviewer who has READ the resume, not generic template questions.
            5. Ask follow-up style questions that probe deeper into claimed achievements (e.g., "You mentioned reducing API response times by 60% at StartupXYZ — walk me through the specific optimizations you applied").
            6. For technical questions, ask about real-world tradeoffs and decisions, not textbook definitions.
            7. Include questions that challenge or verify specific claims in the resume.
            8. ANTI-HALLUCINATION: Only reference facts explicitly stated in the resume. Do NOT infer, invent, or assume projects, metrics, technologies, or achievements that are not mentioned. If the resume is vague about a detail, ask the candidate to elaborate — do not fill in the blanks yourself.

            Difficulty distribution (infer career level from resume):
            - Junior: 40% Easy, 40% Medium, 20% Hard
            - Mid: 25% Easy, 50% Medium, 25% Hard
            - Senior: 15% Easy, 35% Medium, 50% Hard

            Categories: Technical, Situational, Domain Knowledge, Problem Solving, Leadership, Communication
            Timing: ts = thinking seconds, as = answering seconds
            - Easy: ts 15-30, as 30-60. Medium: ts 30-60, as 60-120. Hard: ts 45-90, as 90-180

            Also detect:
            - The resume's language code (e.g. "en", "fr", "ar")
            - The candidate's professional field (e.g. "Software Engineering", "Data Science")

            Resume:
            {{resumeText}}

            JSON format:
            {"questions":[{"q":"question text","c":"Technical","d":"Easy","ts":20,"as":45}],"language":"en","field":"Software Engineering"}

            d must be "Easy", "Medium", or "Hard". Exactly {{count}} question items.
            """;
    }

    private class DeepSeekChatResponse
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

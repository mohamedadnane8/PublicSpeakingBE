using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Configuration;

namespace MyApp.Infrastructure.Services;

public class DeepgramTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly DeepgramOptions _options;
    private readonly IS3StorageService _s3StorageService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<DeepgramTranscriptionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeepgramTranscriptionService(
        HttpClient httpClient,
        IOptions<DeepgramOptions> options,
        IS3StorageService s3StorageService,
        ISessionRepository sessionRepository,
        ILogger<DeepgramTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _s3StorageService = s3StorageService;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task TranscribeSessionAsync(
        Guid sessionId,
        string audioObjectKey,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await UpdateSessionStatusAsync(sessionId, "Processing", null, cancellationToken);

            var audioUrl = await _s3StorageService.GetPreSignedGetUrlAsync(
                audioObjectKey,
                TimeSpan.FromMinutes(60),
                cancellationToken);

            _logger.LogInformation("Starting Deepgram transcription for session {SessionId}.", sessionId);

            var transcript = await TranscribeAsync(audioUrl, languageCode, cancellationToken);

            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found after transcription.", sessionId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(transcript))
            {
                session.SetTranscript(transcript);
                session.SetTranscriptionStatus("Completed");
                _logger.LogInformation("Deepgram transcription completed for session {SessionId}. Length: {Length} chars.", sessionId, transcript.Length);
            }
            else
            {
                session.SetTranscriptionStatus("Failed", "Deepgram returned empty transcript.");
                _logger.LogWarning("Deepgram returned empty transcript for session {SessionId}.", sessionId);
            }

            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deepgram transcription failed for session {SessionId}.", sessionId);

            try
            {
                await UpdateSessionStatusAsync(sessionId, "Failed", ex.Message, cancellationToken);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update transcription status for session {SessionId}.", sessionId);
            }
        }
    }

    private async Task<string?> TranscribeAsync(
        string audioUrl,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        // Build query string
        var queryParams = new List<string>
        {
            $"model={_options.Model}",
            "filler_words=true",
            "punctuate=true",
            "smart_format=true"
        };

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            queryParams.Add($"language={languageCode}");
        }
        else
        {
            queryParams.Add("detect_language=true");
        }

        var url = $"{_options.BaseUrl}/listen?{string.Join("&", queryParams)}";

        var requestBody = JsonSerializer.Serialize(new { url = audioUrl }, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Token {_options.ApiKey}");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Deepgram failed ({response.StatusCode}): {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<DeepgramResponse>(responseJson, JsonOptions);

        return result?.Results?.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault()?.Transcript;
    }

    private async Task UpdateSessionStatusAsync(
        Guid sessionId,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session != null)
        {
            session.SetTranscriptionStatus(status, error);
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);
        }
    }

    // Response models
    private class DeepgramResponse
    {
        public DeepgramResults? Results { get; set; }
    }

    private class DeepgramResults
    {
        public List<DeepgramChannel>? Channels { get; set; }
    }

    private class DeepgramChannel
    {
        public List<DeepgramAlternative>? Alternatives { get; set; }
    }

    private class DeepgramAlternative
    {
        public string? Transcript { get; set; }
        public double Confidence { get; set; }
    }
}

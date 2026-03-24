using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Interfaces;
using MyApp.Infrastructure.Configuration;

namespace MyApp.Infrastructure.Services;

public class AssemblyAITranscriptionService : ITranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly AssemblyAIOptions _options;
    private readonly IS3StorageService _s3StorageService;
    private readonly ISessionRepository _sessionRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyAITranscriptionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AssemblyAITranscriptionService(
        HttpClient httpClient,
        IOptions<AssemblyAIOptions> options,
        IS3StorageService s3StorageService,
        ISessionRepository sessionRepository,
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyAITranscriptionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _s3StorageService = s3StorageService;
        _sessionRepository = sessionRepository;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task TranscribeSessionAsync(
        Guid sessionId,
        string audioObjectKey,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        // Fail fast if API key is not configured
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("AssemblyAI API key is not configured. Skipping transcription for session {SessionId}.", sessionId);
            await UpdateSessionTranscriptionStatusAsync(sessionId, "Failed", "Transcription service not configured.", cancellationToken);
            return;
        }

        try
        {
            // Update status to Processing
            await UpdateSessionTranscriptionStatusAsync(sessionId, "Processing", null, cancellationToken);

            // 1. Generate pre-signed S3 URL for AssemblyAI to download
            var audioUrl = await _s3StorageService.GetPreSignedGetUrlAsync(
                audioObjectKey,
                TimeSpan.FromMinutes(_options.PreSignedUrlExpiryMinutes),
                cancellationToken);

            _logger.LogInformation("Starting transcription for session {SessionId}.", sessionId);

            // 2. Submit transcription request
            var transcriptId = await SubmitTranscriptionAsync(audioUrl, languageCode, cancellationToken);

            _logger.LogInformation("Transcription submitted for session {SessionId}, transcript ID: {TranscriptId}.", sessionId, transcriptId);

            // 3. Poll for completion
            var result = await PollForCompletionAsync(transcriptId, cancellationToken);

            // 4. Store result on session (fresh load to avoid tracking conflicts)
            var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found after transcription completed.", sessionId);
                return;
            }

            if (result.Status == "completed")
            {
                session.SetTranscript(result.Text);
                session.SetTranscriptionStatus("Completed");
                _logger.LogInformation("Transcription completed for session {SessionId}. Length: {Length} chars.", sessionId, result.Text?.Length ?? 0);
            }
            else
            {
                session.SetTranscriptionStatus("Failed", result.Error ?? "Unknown transcription error");
                _logger.LogWarning("Transcription failed for session {SessionId}: {Error}.", sessionId, result.Error);
            }

            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed for session {SessionId}.", sessionId);

            // Use a fresh scope to avoid entity tracking conflicts
            try
            {
                await UpdateSessionTranscriptionStatusFreshAsync(sessionId, "Failed", ex.Message);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update transcription status for session {SessionId}.", sessionId);
            }
        }
    }

    private async Task<string> SubmitTranscriptionAsync(
        string audioUrl,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["audio_url"] = audioUrl,
            ["disfluencies"] = true
        };

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            requestBody["language_code"] = languageCode;
        }
        else
        {
            requestBody["language_detection"] = true;
        }

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/transcript")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", _options.ApiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"AssemblyAI submit failed ({response.StatusCode}): {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TranscriptResponse>(responseJson, JsonOptions);

        return result?.Id ?? throw new InvalidOperationException("AssemblyAI did not return a transcript ID.");
    }

    private async Task<TranscriptResult> PollForCompletionAsync(
        string transcriptId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _options.MaxPollingAttempts; attempt++)
        {
            await Task.Delay(_options.PollingIntervalMs, cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/transcript/{transcriptId}");
            request.Headers.Add("Authorization", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TranscriptResponse>(responseJson, JsonOptions);

            if (result == null)
                continue;

            switch (result.Status)
            {
                case "completed":
                    return new TranscriptResult("completed", result.Text, null);

                case "error":
                    return new TranscriptResult("error", null, result.Error ?? "Unknown error");

                case "queued":
                case "processing":
                    _logger.LogDebug("Transcription {Id} status: {Status} (attempt {Attempt}).", transcriptId, result.Status, attempt + 1);
                    continue;

                default:
                    _logger.LogWarning("Transcription {Id} unknown status: {Status}.", transcriptId, result.Status);
                    continue;
            }
        }

        throw new TimeoutException($"Transcription {transcriptId} did not complete within {_options.MaxPollingAttempts} polling attempts.");
    }

    private async Task UpdateSessionTranscriptionStatusAsync(
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

    /// <summary>
    /// Uses a fresh DI scope to avoid entity tracking conflicts when the main scope's
    /// DbContext already has the Session loaded.
    /// </summary>
    private async Task UpdateSessionTranscriptionStatusFreshAsync(
        Guid sessionId,
        string status,
        string? error)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var session = await repo.GetByIdAsync(sessionId);
        if (session != null)
        {
            session.SetTranscriptionStatus(status, error);
            await repo.UpdateAsync(session);
            await repo.SaveChangesAsync();
        }
    }

    private class TranscriptResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
    }

    private record TranscriptResult(string Status, string? Text, string? Error);
}

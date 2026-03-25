using System.Net.Http.Headers;
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

    // EBML header magic bytes (webm/matroska container)
    private static readonly byte[] EbmlMagic = [0x1A, 0x45, 0xDF, 0xA3];

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
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found, skipping transcription", sessionId);
            return;
        }

        try
        {
            session.SetTranscriptionStatus("Processing");
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);

            // Download audio from S3
            var (audioStream, contentType, contentLength) = await _s3StorageService.DownloadAsync(
                audioObjectKey, cancellationToken);

            await using (audioStream)
            {
                var extension = Path.GetExtension(audioObjectKey)?.ToLowerInvariant() ?? "unknown";
                _logger.LogInformation(
                    "Starting Deepgram transcription for session {SessionId} (extension={Extension}, contentType={ContentType}, size={Size}, durationMs={DurationMs})",
                    sessionId, extension, contentType, contentLength, session.AudioDurationMs);

                // Buffer the S3 stream into a byte array for reliable sending
                using var ms = new MemoryStream();
                await audioStream.CopyToAsync(ms, cancellationToken);
                var audioBytes = ms.ToArray();

                var transcript = await TranscribeWithRetryAsync(audioBytes, contentType, languageCode, cancellationToken);

                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    session.SetTranscript(transcript);
                    session.SetTranscriptionStatus("Completed");
                    _logger.LogInformation("Deepgram transcription completed for session {SessionId} ({Length} chars)",
                        sessionId, transcript.Length);
                }
                else
                {
                    session.SetTranscriptionStatus("Failed", "Deepgram returned empty transcript.");
                    _logger.LogWarning("Deepgram returned empty transcript for session {SessionId}", sessionId);
                }
            }

            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _sessionRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deepgram transcription failed for session {SessionId}", sessionId);

            try
            {
                session.SetTranscriptionStatus("Failed", ex.Message);
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _sessionRepository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update transcription status for session {SessionId}", sessionId);
            }
        }
    }

    public async Task<string?> TranscribeAudioAsync(
        Stream audioStream,
        string contentType,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        // Copy stream to byte array so we can safely retry and validate
        using var ms = new MemoryStream();
        await audioStream.CopyToAsync(ms, cancellationToken);
        var audioBytes = ms.ToArray();

        return await TranscribeWithRetryAsync(audioBytes, contentType, languageCode, cancellationToken);
    }

    /// <summary>
    /// Sends audio bytes to Deepgram with retries on 400 errors.
    /// Strategy:
    ///   1. Try with original content-type + language
    ///   2. Try with detect_language=true (no fixed language hint)
    ///   3. Try with no content-type at all (let Deepgram auto-detect format)
    /// </summary>
    private async Task<string?> TranscribeWithRetryAsync(
        byte[] audioBytes,
        string contentType,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        ValidateAudioData(audioBytes, contentType);

        _logger.LogInformation(
            "Deepgram transcription starting (size={Size}, contentType={ContentType}, language={Language}, header={Header})",
            audioBytes.Length, contentType, languageCode ?? "auto-detect",
            BitConverter.ToString(audioBytes, 0, Math.Min(8, audioBytes.Length)));

        // Attempt 1: original content-type + language
        try
        {
            return await SendToDeepgramAsync(audioBytes, contentType, languageCode, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("BadRequest") || ex.Message.Contains("400"))
        {
            _logger.LogWarning(
                "Deepgram 400 on attempt 1 (contentType={ContentType}, language={Language}). Retrying with detect_language=true",
                contentType, languageCode ?? "auto-detect");
        }

        // Attempt 2: original content-type + auto language detection
        try
        {
            return await SendToDeepgramAsync(audioBytes, contentType, null, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("BadRequest") || ex.Message.Contains("400"))
        {
            _logger.LogWarning(
                "Deepgram 400 on attempt 2 (contentType={ContentType}, language=auto). Retrying with no content-type (full auto-detect)",
                contentType);
        }

        // Attempt 3: no content-type at all — let Deepgram sniff the format from the bytes
        try
        {
            return await SendToDeepgramAsync(audioBytes, null, null, cancellationToken);
        }
        catch (HttpRequestException retryEx)
        {
            _logger.LogError(retryEx,
                "Deepgram all 3 attempts failed (size={Size}, originalContentType={ContentType})",
                audioBytes.Length, contentType);
            throw;
        }
    }

    private void ValidateAudioData(byte[] audioBytes, string contentType)
    {
        if (audioBytes.Length == 0)
            throw new InvalidOperationException("Audio data is empty (0 bytes)");

        if (audioBytes.Length < 100)
        {
            _logger.LogWarning("Audio data suspiciously small ({Size} bytes), may produce empty transcript", audioBytes.Length);
        }

        // Check EBML header for webm files
        if (contentType.Contains("webm", StringComparison.OrdinalIgnoreCase) && audioBytes.Length >= 4)
        {
            if (audioBytes[0] != EbmlMagic[0] || audioBytes[1] != EbmlMagic[1] ||
                audioBytes[2] != EbmlMagic[2] || audioBytes[3] != EbmlMagic[3])
            {
                _logger.LogWarning(
                    "WebM audio does not start with EBML header (got {Header}). Audio may be corrupted",
                    BitConverter.ToString(audioBytes, 0, Math.Min(8, audioBytes.Length)));
            }
        }
    }

    private async Task<string?> SendToDeepgramAsync(
        byte[] audioBytes,
        string? contentType,
        string? languageCode,
        CancellationToken cancellationToken)
    {
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

        // Use ByteArrayContent for reliable Content-Length and no stream position issues
        using var content = new ByteArrayContent(audioBytes);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("Authorization", $"Token {_options.ApiKey}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Deepgram returned {StatusCode} after {ElapsedMs}ms: {ErrorBody}",
                response.StatusCode, sw.ElapsedMilliseconds, errorBody);
            throw new HttpRequestException($"Deepgram failed ({response.StatusCode}): {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        sw.Stop();

        _logger.LogInformation("Deepgram responded OK in {ElapsedMs}ms (language={Language})",
            sw.ElapsedMilliseconds, languageCode ?? "auto-detect");

        var result = JsonSerializer.Deserialize<DeepgramResponse>(responseJson, JsonOptions);
        return result?.Results?.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault()?.Transcript;
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

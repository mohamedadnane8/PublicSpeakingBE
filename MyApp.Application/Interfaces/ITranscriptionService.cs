namespace MyApp.Application.Interfaces;

public interface ITranscriptionService
{
    /// <summary>
    /// Transcribe audio already stored in S3. Updates the session entity directly.
    /// </summary>
    Task TranscribeSessionAsync(
        Guid sessionId,
        string audioObjectKey,
        string? languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribe raw audio bytes directly. Returns the transcript string (or null).
    /// Does not touch the database — caller is responsible for storing the result.
    /// </summary>
    Task<string?> TranscribeAudioAsync(
        Stream audioStream,
        string contentType,
        string? languageCode,
        CancellationToken cancellationToken = default);
}

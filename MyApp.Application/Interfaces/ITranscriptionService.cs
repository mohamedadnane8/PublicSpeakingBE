namespace MyApp.Application.Interfaces;

public interface ITranscriptionService
{
    Task TranscribeSessionAsync(
        Guid sessionId,
        string audioObjectKey,
        string? languageCode,
        CancellationToken cancellationToken = default);
}

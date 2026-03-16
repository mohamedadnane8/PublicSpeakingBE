using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IS3StorageService
{
    Task<AudioUploadResultDto> UploadAudioAsync(
        Guid userId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<string> GetPreSignedGetUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}

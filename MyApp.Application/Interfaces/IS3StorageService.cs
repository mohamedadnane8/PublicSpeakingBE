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
}

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

    /// <summary>
    /// Download a file from S3 and return the stream with its content type.
    /// Caller is responsible for disposing the stream.
    /// </summary>
    Task<(Stream Stream, string ContentType, long ContentLength)> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}

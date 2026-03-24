using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.Infrastructure.Storage;

public class S3StorageService : IS3StorageService
{
    private static readonly Regex InvalidFileNameChars = new(@"[^a-zA-Z0-9\-_]+", RegexOptions.Compiled);

    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3StorageService> _logger;
    private readonly S3StorageOptions _options;

    public S3StorageService(
        IAmazonS3 s3Client,
        IOptions<S3StorageOptions> options,
        ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException("S3Storage:BucketName is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Region))
        {
            throw new InvalidOperationException("S3Storage:Region is required.");
        }
    }

    public async Task<AudioUploadResultDto> UploadAudioAsync(
        Guid userId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));

        if (!fileStream.CanRead)
            throw new ArgumentException("File stream is not readable.", nameof(fileStream));

        if (fileSize <= 0)
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSize));

        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));

        var objectKey = BuildObjectKey(userId, originalFileName);
        var safeContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = safeContentType,
            AutoCloseStream = false
        };

        _logger.LogInformation(
            "Uploading audio to S3. Bucket: {Bucket}, Key: {Key}, Size: {Size}",
            _options.BucketName,
            objectKey,
            fileSize);

        await _s3Client.PutObjectAsync(putRequest, cancellationToken);

        _logger.LogInformation("Audio uploaded to S3 successfully. Key: {Key}, Size: {Size} bytes", objectKey, fileSize);

        return new AudioUploadResultDto
        {
            ObjectKey = objectKey,
            BucketName = _options.BucketName,
            Region = _options.Region,
            FileSize = fileSize,
            ContentType = safeContentType
        };
    }

    public Task<string> GetPreSignedGetUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key is required.", nameof(objectKey));

        var ttl = expiresIn <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : expiresIn;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey.Trim(),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    private static string BuildObjectKey(Guid userId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        var baseFileName = Path.GetFileNameWithoutExtension(originalFileName);

        var sanitizedBaseName = InvalidFileNameChars.Replace(baseFileName, "-").Trim('-');
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            sanitizedBaseName = "audio";
        }

        if (sanitizedBaseName.Length > 80)
        {
            sanitizedBaseName = sanitizedBaseName[..80];
        }

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("N");

        return $"audio/{userId}/{now:yyyy}/{now:MM}/{id}-{sanitizedBaseName}{extension}";
    }
}

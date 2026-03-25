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
    private static readonly Regex InvalidExtensionChars = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> ExtensionByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/mpeg"] = ".mp3",
            ["audio/mp3"] = ".mp3",
            ["audio/wav"] = ".wav",
            ["audio/x-wav"] = ".wav",
            ["audio/wave"] = ".wav",
            ["audio/vnd.wave"] = ".wav",
            ["audio/mp4"] = ".m4a",
            ["audio/x-m4a"] = ".m4a",
            ["audio/m4a"] = ".m4a",
            ["audio/webm"] = ".webm",
            ["audio/ogg"] = ".ogg",
            ["audio/opus"] = ".ogg"
        };

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

        var safeContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
        var objectKey = BuildObjectKey(userId, originalFileName, safeContentType);

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = safeContentType,
            AutoCloseStream = false
        };

        _logger.LogInformation(
            "Uploading audio to S3. Bucket: {Bucket}, Key: {Key}, ContentType: {ContentType}, Size: {Size}",
            _options.BucketName,
            objectKey,
            safeContentType,
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

    public async Task<(Stream Stream, string ContentType, long ContentLength)> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Object key is required.", nameof(objectKey));

        var request = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey.Trim()
        };

        var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        var contentType = response.Headers.ContentType ?? "application/octet-stream";
        var contentLength = response.ContentLength;

        _logger.LogInformation("Downloaded from S3: {Key} (contentType={ContentType}, size={Size})",
            objectKey, contentType, contentLength);

        return (response.ResponseStream, contentType, contentLength);
    }

    private static string BuildObjectKey(Guid userId, string originalFileName, string contentType)
    {
        var extension = ResolveExtension(originalFileName, contentType);
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("N");

        // Keep key short and deterministic to avoid DB column-size issues in older environments.
        return $"audio/{userId:N}/{now:yyyy}/{now:MM}/{id}{extension}";
    }

    private static string ResolveExtension(string originalFileName, string contentType)
    {
        var normalizedContentType = contentType.Split(';', 2)[0].Trim();
        if (ExtensionByContentType.TryGetValue(normalizedContentType, out var mappedExtension))
        {
            return mappedExtension;
        }

        var rawExtension = Path.GetExtension(originalFileName)?.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(rawExtension))
        {
            return ".bin";
        }

        var sanitized = InvalidExtensionChars.Replace(rawExtension, string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return ".bin";
        }

        if (sanitized.Length > 10)
        {
            sanitized = sanitized[..10];
        }

        return $".{sanitized}";
    }
}

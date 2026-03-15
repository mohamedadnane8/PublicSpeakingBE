using System.Security.Claims;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.API.Controllers;

[ApiController]
[Route("api/audio")]
[Authorize]
public class AudioController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".webm", ".ogg"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedContentTypesByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp3"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio/mpeg",
                "audio/mp3"
            },
            [".wav"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio/wav",
                "audio/x-wav",
                "audio/wave",
                "audio/vnd.wave"
            },
            [".m4a"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio/mp4",
                "audio/x-m4a",
                "audio/m4a"
            },
            [".webm"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio/webm"
            },
            [".ogg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audio/ogg",
                "audio/opus"
            }
        };

    private readonly IS3StorageService _s3StorageService;
    private readonly ILogger<AudioController> _logger;

    public AudioController(
        IS3StorageService s3StorageService,
        ILogger<AudioController> logger)
    {
        _s3StorageService = s3StorageService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AudioUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AudioUploadResultDto>> UploadAudio(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { error = "file_required", message = "No file was provided." });
        }

        if (file.Length <= 0)
        {
            return BadRequest(new { error = "empty_file", message = "Uploaded file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new
            {
                error = "file_too_large",
                message = "File size exceeds the 10 MB limit."
            });
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                error = "invalid_extension",
                message = "Only .mp3, .wav, .m4a, .webm, and .ogg files are allowed."
            });
        }

        if (!IsAllowedAudioContentType(extension, file.ContentType))
        {
            return BadRequest(new
            {
                error = "invalid_content_type",
                message = $"Invalid content type '{file.ContentType}' for extension '{extension}'."
            });
        }

        Guid userId;
        try
        {
            userId = GetCurrentUserId();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Audio upload failed: unable to resolve user id from token.");
            return Unauthorized(new { error = "unauthorized", message = "User is not authenticated." });
        }

        try
        {
            await using var stream = file.OpenReadStream();

            var result = await _s3StorageService.UploadAudioAsync(
                userId: userId,
                fileStream: stream,
                originalFileName: file.FileName,
                contentType: file.ContentType,
                fileSize: file.Length,
                cancellationToken: cancellationToken);

            return Ok(result);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 upload failed for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "storage_upload_failed",
                message = "Failed to upload audio to storage provider."
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid upload request for user {UserId}.", userId);
            return BadRequest(new
            {
                error = "invalid_upload_request",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during audio upload for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "unexpected_error",
                message = "Unexpected error while uploading audio."
            });
        }
    }

    private static bool IsAllowedAudioContentType(string extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalizedContentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();

        if (!normalizedContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes) &&
               allowedContentTypes.Contains(normalizedContentType);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("User ID not found in token.");
        }

        return userId;
    }
}

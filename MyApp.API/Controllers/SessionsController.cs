using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private const long MaxAudioSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedAudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp3", "audio/wav", "audio/x-wav", "audio/wave",
        "audio/mp4", "audio/x-m4a", "audio/m4a", "audio/webm", "audio/ogg", "audio/opus"
    };

    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SessionDto>> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var session = await _sessionService.CreateSessionAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    /// <summary>
    /// Create a session with audio in one request. Uploads audio to S3 and creates the session.
    /// Transcription is NOT done here — call POST /api/sessions/{id}/transcribe separately.
    /// </summary>
    [HttpPost("record")]
    [RequestSizeLimit(MaxAudioSizeBytes + 1024 * 100)] // audio + JSON overhead
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAudioSizeBytes + 1024 * 100)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDto>> CreateSessionWithAudio(
        [FromForm] IFormFile audio,
        [FromForm] string session,
        CancellationToken cancellationToken)
    {
        // Parse session JSON
        CreateSessionRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateSessionRequest>(session, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = "invalid_session_data", message = $"Invalid session JSON: {ex.Message}" });
        }

        if (request == null)
            return BadRequest(new { error = "missing_session_data", message = "Session data is required." });

        // Validate audio
        if (audio == null || audio.Length <= 0)
            return BadRequest(new { error = "file_required", message = "Audio file is required." });

        if (audio.Length > MaxAudioSizeBytes)
            return BadRequest(new { error = "file_too_large", message = "Audio file exceeds the 10 MB limit." });

        var normalizedContentType = audio.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedAudioContentTypes.Contains(normalizedContentType))
        {
            return BadRequest(new
            {
                error = "invalid_content_type",
                message = $"Unsupported audio type '{audio.ContentType}'. Supported: mp3, wav, m4a, webm, ogg."
            });
        }

        var userId = GetCurrentUserId();

        await using var stream = audio.OpenReadStream();
        var result = await _sessionService.CreateSessionWithAudioAsync(
            userId, request, stream, audio.FileName, normalizedContentType, audio.Length, cancellationToken);

        return CreatedAtAction(nameof(GetSession), new { id = result.Id }, result);
    }

    /// <summary>
    /// Trigger background transcription for a session that already has audio in S3.
    /// Returns 202 Accepted immediately — transcription runs asynchronously.
    /// Poll GET /api/sessions/{id} to check transcriptionStatus.
    /// </summary>
    [HttpPost("{id:guid}/transcribe")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TranscribeSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var triggered = await _sessionService.TriggerTranscriptionAsync(id, userId, cancellationToken);

        if (!triggered)
            return NotFound(new { error = "not_found", message = "Session not found, not owned by you, or has no audio." });

        return Accepted(new { message = "Transcription started. Poll GET /api/sessions/{id} for status." });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionDto>> UpdateSession(
        Guid id,
        [FromBody] UpdateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var session = await _sessionService.UpdateSessionAsync(id, userId, request, cancellationToken);
        if (session == null)
            return NotFound(new { error = "Session not found or access denied" });

        return Ok(session);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> GetSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(id, cancellationToken);
        if (session == null)
            return NotFound(new { error = "Session not found" });

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
            return Forbid();

        return Ok(session);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetMySessions(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var sessions = await _sessionService.GetUserSessionsAsync(userId, cancellationToken);
        return Ok(sessions);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(id, cancellationToken);
        if (session == null)
            return NotFound(new { error = "Session not found" });

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
            return Forbid();

        await _sessionService.DeleteSessionAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/audio-url")]
    [ProducesResponseType(typeof(AudioPlaybackUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AudioPlaybackUrlDto>> GetSessionAudioUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await _sessionService.GetSessionAsync(id, cancellationToken);
        if (session == null)
            return NotFound(new { error = "Session not found" });

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
            return Forbid();

        var audioUrl = await _sessionService.GetAudioUrlAsync(id, cancellationToken);
        if (audioUrl == null)
            return NotFound(new { error = "Audio not available for this session" });

        return Ok(audioUrl);
    }

    [HttpPost("{id:guid}/analyze")]
    [ProducesResponseType(typeof(SpeechAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> AnalyzeSession(
        Guid id,
        [FromQuery] bool reanalyze = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            var result = await _sessionService.AnalyzeSessionAsync(id, userId, reanalyze, cancellationToken);

            if (result.Success)
                return Ok(result.Analysis);

            if (result.RateLimited)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    error = result.Error,
                    message = result.ErrorMessage,
                    analysesUsed = result.AnalysesUsed,
                    maxPerDay = result.MaxPerDay,
                    resetsAt = result.ResetsAt
                });
            }

            return StatusCode(result.HttpStatus ?? 400, new
            {
                error = result.Error,
                message = result.ErrorMessage
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI service call failed during analysis of session {SessionId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "ai_service_unavailable",
                message = "The AI service is currently unavailable. Please try again later."
            });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new InvalidOperationException("User ID not found in token");

        return userId;
    }
}

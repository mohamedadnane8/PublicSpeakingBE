using System.Security.Claims;
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
    [HttpPost("record")]
    public async Task<ActionResult<SessionDto>> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var session = await _sessionService.CreateSessionAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
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

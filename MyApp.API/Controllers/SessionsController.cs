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
    private static readonly TimeSpan AudioUrlTtl = TimeSpan.FromMinutes(15);

    private readonly ISessionService _sessionService;
    private readonly IS3StorageService _s3StorageService;

    public SessionsController(
        ISessionService sessionService,
        IS3StorageService s3StorageService)
    {
        _sessionService = sessionService;
        _s3StorageService = s3StorageService;
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
        {
            return NotFound(new { error = "Session not found" });
        }

        // Ensure user can only access their own sessions
        var userId = GetCurrentUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

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
        {
            return NotFound(new { error = "Session not found" });
        }

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

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
        {
            return NotFound(new { error = "Session not found" });
        }

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
        {
            return Forbid();
        }

        var objectKey = session.Audio?.ObjectKey;
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return NotFound(new { error = "Audio not available for this session" });
        }

        var url = await _s3StorageService.GetPreSignedGetUrlAsync(objectKey, AudioUrlTtl, cancellationToken);
        return Ok(new AudioPlaybackUrlDto
        {
            Url = url,
            ExpiresAt = DateTime.UtcNow.Add(AudioUrlTtl)
        });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("User ID not found in token");
        }

        return userId;
    }
}

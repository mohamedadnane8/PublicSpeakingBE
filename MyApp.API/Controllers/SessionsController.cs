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

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
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

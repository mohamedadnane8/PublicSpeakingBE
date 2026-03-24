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
    private const int MaxAiAnalysesPerDay = 3;

    private readonly ISessionService _sessionService;
    private readonly ISessionRepository _sessionRepository;
    private readonly IS3StorageService _s3StorageService;
    private readonly ISpeechAnalysisService _speechAnalysisService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ISessionRepository sessionRepository,
        IS3StorageService s3StorageService,
        ISpeechAnalysisService speechAnalysisService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _sessionRepository = sessionRepository;
        _s3StorageService = s3StorageService;
        _speechAnalysisService = speechAnalysisService;
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

    /// <summary>
    /// Analyze the speech transcript of a completed session.
    /// Uses different evaluation frameworks for General (public speaking) vs Interview (STAR) sessions.
    /// Results are stored on the session and cached — subsequent calls return the cached analysis.
    /// Pass ?reanalyze=true to force a fresh analysis.
    /// </summary>
    [HttpPost("{id:guid}/analyze")]
    [ProducesResponseType(typeof(SpeechAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SpeechAnalysisResultDto>> AnalyzeSession(
        Guid id,
        [FromQuery] bool reanalyze = false,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionService.GetSessionAsync(id, cancellationToken);
        if (session == null)
            return NotFound(new { error = "session_not_found", message = "Session not found." });

        var userId = GetCurrentUserId();
        if (session.UserId != userId)
            return Forbid();

        // Return cached analysis if available and not forcing reanalysis
        if (!reanalyze && session.SpeechAnalysis != null)
        {
            // Cached — no rate limit consumed

            return Ok(new SpeechAnalysisResultDto
            {
                SessionType = session.Type,
                GeneralAnalysis = session.Type != "Interview"
                    ? System.Text.Json.JsonSerializer.Deserialize<GeneralSpeechAnalysisDto>(
                        System.Text.Json.JsonSerializer.Serialize(session.SpeechAnalysis))
                    : null,
                InterviewAnalysis = session.Type == "Interview"
                    ? System.Text.Json.JsonSerializer.Deserialize<InterviewSpeechAnalysisDto>(
                        System.Text.Json.JsonSerializer.Serialize(session.SpeechAnalysis))
                    : null
            });
        }

        // Rate limit: max 3 AI analyses per day (cached calls don't count)
        var analysesToday = await _sessionRepository.CountAiAnalysesTodayAsync(userId, cancellationToken);
        if (analysesToday >= MaxAiAnalysesPerDay)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "daily_analysis_limit",
                message = $"You have reached the limit of {MaxAiAnalysesPerDay} AI analyses per day.",
                analysesUsed = analysesToday,
                maxPerDay = MaxAiAnalysesPerDay,
                resetsAt = DateTime.UtcNow.Date.AddDays(1)
            });
        }

        if (string.IsNullOrWhiteSpace(session.Transcript))
        {
            return BadRequest(new
            {
                error = "no_transcript",
                message = "Session has no transcript to analyze. Wait for transcription to complete or provide a transcript."
            });
        }

        try
        {
            var result = new SpeechAnalysisResultDto { SessionType = session.Type };
            string analysisJson;

            if (session.Type == "Interview")
            {
                var analysis = await _speechAnalysisService.AnalyzeInterviewSpeechAsync(
                    session.Word, session.Transcript, cancellationToken);
                result.InterviewAnalysis = analysis;
                analysisJson = System.Text.Json.JsonSerializer.Serialize(analysis);
            }
            else
            {
                var analysis = await _speechAnalysisService.AnalyzeGeneralSpeechAsync(
                    session.Transcript, cancellationToken);
                result.GeneralAnalysis = analysis;
                analysisJson = System.Text.Json.JsonSerializer.Serialize(analysis);
            }

            // Store analysis on the session
            await _sessionService.SetSpeechAnalysisAsync(id, analysisJson, cancellationToken);

            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DeepSeek API call failed for session analysis {SessionId}.", id);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "ai_service_unavailable",
                message = "The AI service is currently unavailable. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech analysis failed for session {SessionId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "analysis_failed",
                message = "Failed to analyze the speech."
            });
        }
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.API.Controllers;

[ApiController]
[Route("api/feature-requests")]
[Authorize]
public class FeatureRequestsController : ControllerBase
{
    private readonly IFeatureRequestService _featureRequestService;

    public FeatureRequestsController(IFeatureRequestService featureRequestService)
    {
        _featureRequestService = featureRequestService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(FeatureRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FeatureRequestDto>> Create(
        [FromBody] CreateFeatureRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var created = await _featureRequestService.CreateAsync(userId, request, cancellationToken);
        return Created($"/api/feature-requests/{created.Id}", created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<FeatureRequestDto>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var requests = await _featureRequestService.GetForUserAsync(userId, cancellationToken);
        return Ok(requests);
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

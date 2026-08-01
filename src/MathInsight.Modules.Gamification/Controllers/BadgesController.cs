using System.Security.Claims;
using MathInsight.Modules.Gamification.Queries.Badges;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Gamification.Controllers;

[ApiController]
[Route("api/v1/gamification/badges")]
[Authorize(Roles = "Student")]
public sealed class BadgesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BadgesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<BadgeDto>>> GetBadges(CancellationToken cancellationToken)
    {
        var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var query = new GetBadgeListQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<ActionResult<List<BadgeProgressDto>>> GetBadgeProgress(CancellationToken cancellationToken)
    {
        var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var query = new GetBadgeProgressQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
}

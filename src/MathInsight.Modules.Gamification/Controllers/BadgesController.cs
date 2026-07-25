using MathInsight.Modules.Gamification.Queries.Badges;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Gamification.Controllers;

[ApiController]
[Route("api/v1/gamification/badges")]
// [Authorize]
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
        // var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var studentId = "student-123"; 

        var query = new GetBadgeListQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<ActionResult<List<BadgeProgressDto>>> GetBadgeProgress(CancellationToken cancellationToken)
    {
        // var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var studentId = "student-123"; 

        var query = new GetBadgeProgressQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }
}

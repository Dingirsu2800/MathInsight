using MathInsight.Modules.Notification_Report.Queries.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Notification_Report.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "Student")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>UC-58: daily-cached class ranking for a grade.</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int grade, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaderboardQuery(grade), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error?.Message });
        }

        return Ok(result.Value);
    }
}

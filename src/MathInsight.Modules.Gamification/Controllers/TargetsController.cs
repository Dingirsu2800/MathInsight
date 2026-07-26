using System.Security.Claims;
using MathInsight.Modules.Gamification.Commands.TargetScores;
using MathInsight.Modules.Gamification.Queries.TargetScores;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Gamification.Controllers;

[ApiController]
[Route("api/v1/gamification/targets")]
[Authorize(Roles = "Student")]
public sealed class TargetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TargetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<TargetProgressDto>>> GetMyTargets(CancellationToken cancellationToken)
    {
        var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var query = new GetTargetProgressQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<string>> CreateTarget([FromBody] CreateTargetRequest request, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var command = new SetTargetScoreCommand(studentId, request.TagId, request.TargetPoint);
        var targetId = await _mediator.Send(command, cancellationToken);

        return Ok(new { TargetId = targetId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTarget(string id, [FromBody] UpdateTargetRequest request, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var command = new UpdateTargetScoreCommand(id, studentId, request.TargetPoint);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}

public class CreateTargetRequest
{
    public string TagId { get; set; } = default!;
    public decimal TargetPoint { get; set; }
}

public class UpdateTargetRequest
{
    public decimal TargetPoint { get; set; }
}

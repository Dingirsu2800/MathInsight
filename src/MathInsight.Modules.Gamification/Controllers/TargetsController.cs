using MathInsight.Modules.Gamification.Commands.TargetScores;
using MathInsight.Modules.Gamification.Queries.TargetScores;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Gamification.Controllers;

[ApiController]
[Route("api/v1/gamification/targets")]
// [Authorize] - Assuming auth is handled via API gateway or global filters for this snippet
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
        // In a real app, retrieve StudentId from User Claims
        // var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Using a dummy value for demonstration
        var studentId = "student-123"; 
        
        var query = new GetTargetProgressQuery(studentId);
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<string>> CreateTarget([FromBody] CreateTargetRequest request, CancellationToken cancellationToken)
    {
        // var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var studentId = "student-123"; 

        var command = new SetTargetScoreCommand(studentId, request.TagId, request.TargetPoint);
        var targetId = await _mediator.Send(command, cancellationToken);
        
        return Ok(new { TargetId = targetId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTarget(string id, [FromBody] UpdateTargetRequest request, CancellationToken cancellationToken)
    {
        // var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var studentId = "student-123"; 

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

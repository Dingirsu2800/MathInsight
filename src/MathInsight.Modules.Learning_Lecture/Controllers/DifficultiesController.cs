using MathInsight.Modules.Learning_Lecture.Queries.Difficulties;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Learning_Lecture.Controllers;

[ApiController]
[Route("api/v1/difficulties")]
[Authorize]
public sealed class DifficultiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DifficultiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetDifficulties(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDifficultyListQuery(), cancellationToken);
        return Ok(result);
    }
}

using MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;
using MathInsight.Modules.QuestionBank.Queries.GetTagTopics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.QuestionBank.Controllers;

[ApiController]
[Authorize(Roles = "Expert")]
[Route("api/question-bank/tags")]
public class TagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("difficulties")]
    public async Task<IActionResult> GetDifficulties(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTagDifficultiesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics([FromQuery] int? grade, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTagTopicTreeQuery(grade), cancellationToken);
        return Ok(result);
    }
}

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Queries.Topics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Learning_Lecture.Controllers;

[ApiController]
[Route("api/v1/topics")]
[Authorize]
public class TopicsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TopicsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetTopics([FromQuery] int? grade, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetTopicListQuery(grade), cancellationToken);
        return Ok(result);
    }
}

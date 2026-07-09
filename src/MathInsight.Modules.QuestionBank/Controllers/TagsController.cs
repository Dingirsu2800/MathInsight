using MathInsight.Modules.QuestionBank.Commands.CreateTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.CreateTagTopic;
using MathInsight.Modules.QuestionBank.Commands.DeleteTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.DeleteTagTopic;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;
using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;
using MathInsight.Modules.QuestionBank.Queries.GetTagTopics;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    [HttpPost("difficulties")]
    public async Task<IActionResult> CreateDifficulty(
        [FromBody] CreateTagDifficultyRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.TagRequestInvalid));

        var result = await _mediator.Send(new CreateTagDifficultyCommand(request), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("difficulties/{difficultyId}")]
    public async Task<IActionResult> UpdateDifficulty(
        string difficultyId,
        [FromBody] UpdateTagDifficultyRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.TagRequestInvalid));

        var result = await _mediator.Send(new UpdateTagDifficultyCommand(difficultyId, request), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [HttpDelete("difficulties/{difficultyId}")]
    public async Task<IActionResult> DeleteDifficulty(
        string difficultyId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteTagDifficultyCommand(difficultyId), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics([FromQuery] int? grade, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTagTopicTreeQuery(grade), cancellationToken);
        return Ok(result);
    }

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic(
        [FromBody] CreateTagTopicRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.TagRequestInvalid));

        var result = await _mediator.Send(new CreateTagTopicCommand(request), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("topics/{tagId}")]
    public async Task<IActionResult> UpdateTopic(
        string tagId,
        [FromBody] UpdateTagTopicRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.TagRequestInvalid));

        var result = await _mediator.Send(new UpdateTagTopicCommand(tagId, request), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [HttpDelete("topics/{tagId}")]
    public async Task<IActionResult> DeleteTopic(
        string tagId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteTagTopicCommand(tagId), cancellationToken);

        if (result.IsFailure)
            return ToTagErrorResult(result.Error!);

        return Ok(result.Value);
    }

    private IActionResult ToTagErrorResult(Error error)
    {
        if (error == QuestionBankErrors.TagTopicNotFound ||
            error == QuestionBankErrors.TagDifficultyNotFound ||
            error == QuestionBankErrors.TagParentNotFound)
        {
            return NotFound(new ApiErrorResponse(error));
        }

        if (error == QuestionBankErrors.TagNameDuplicate ||
            error == QuestionBankErrors.TagLevelValueDuplicate)
        {
            return Conflict(new ApiErrorResponse(error));
        }

        return BadRequest(new ApiErrorResponse(error));
    }
}

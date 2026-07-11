using System.Security.Claims;
using MathInsight.Modules.QuestionBank.Commands.CreateQuestion;
using MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionDetail;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionList;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionVersions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;
using MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;

namespace MathInsight.Modules.QuestionBank.Controllers;

[ApiController]
[Authorize(Roles = "Expert")]
[Route("api/question-bank/questions")]
public class QuestionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuestionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        [FromQuery] string? status,
        [FromQuery] int? grade,
        [FromQuery] string? tagId,
        [FromQuery] string? difficultyId,
        [FromQuery] string? questionType,
        [FromQuery] string? expertId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetQuestionListQuery(
                pageIndex,
                pageSize,
                status,
                grade,
                tagId,
                difficultyId,
                questionType,
                expertId),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ApiErrorResponse(result.Error!));

        return Ok(result.Value);
    }

    [HttpGet("{questionId}")]
    public async Task<IActionResult> GetQuestionDetail(
        string questionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetQuestionDetailQuery(questionId), cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == QuestionBankErrors.QuestionNotFound)
                return NotFound(new ApiErrorResponse(result.Error!));

            return BadRequest(new ApiErrorResponse(result.Error!));
        }

        return Ok(result.Value);
    }

    [HttpGet("{questionId}/versions")]
    public async Task<IActionResult> GetQuestionVersions(
        string questionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetQuestionVersionsQuery(questionId), cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == QuestionBankErrors.QuestionNotFound)
                return NotFound(new ApiErrorResponse(result.Error!));

            return BadRequest(new ApiErrorResponse(result.Error!));
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateQuestion(
        [FromBody] CreateQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.QuestionRequestInvalid));

        var expertId = User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(new CreateQuestionCommand(request, expertId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ApiErrorResponse(result.Error!));

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{questionId}")]
    public async Task<IActionResult> UpdateQuestion(
        string questionId,
        [FromBody] UpdateQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.QuestionRequestInvalid));

        var expertId = User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new UpdateQuestionCommand(questionId, request, expertId),
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == QuestionBankErrors.QuestionNotFound)
                return NotFound(new ApiErrorResponse(result.Error!));

            if (result.Error == QuestionBankErrors.QuestionUpdateForbidden)
                return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(result.Error!));

            return BadRequest(new ApiErrorResponse(result.Error!));
        }

        return Ok(result.Value);
    }

    [HttpPut("{questionId}/active")]
    public async Task<IActionResult> ToggleQuestionActive(
        string questionId,
        [FromBody] ToggleQuestionActiveRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.QuestionRequestInvalid));

        var expertId = User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new ToggleQuestionActiveCommand(questionId, request.IsActive, expertId),
            cancellationToken);

        if (result.IsFailure)
            return ToQuestionMutationErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [HttpDelete("{questionId}")]
    public async Task<IActionResult> DeleteQuestion(
        string questionId,
        CancellationToken cancellationToken)
    {
        var expertId = User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new DeleteQuestionCommand(questionId, expertId),
            cancellationToken);

        if (result.IsFailure)
            return ToQuestionMutationErrorResult(result.Error!);

        return Ok(result.Value);
    }

    private IActionResult ToQuestionMutationErrorResult(Error error)
    {
        if (error == QuestionBankErrors.QuestionNotFound)
            return NotFound(new ApiErrorResponse(error));

        if (error == QuestionBankErrors.QuestionMutationForbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));

        if (error == QuestionBankErrors.QuestionInUse ||
            error == QuestionBankErrors.QuestionHasPendingReports)
            return Conflict(new ApiErrorResponse(error));

        return BadRequest(new ApiErrorResponse(error));
    }
}

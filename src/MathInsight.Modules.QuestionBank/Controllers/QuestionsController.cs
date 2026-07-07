using System.Security.Claims;
using MathInsight.Modules.QuestionBank.Commands.CreateQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
}

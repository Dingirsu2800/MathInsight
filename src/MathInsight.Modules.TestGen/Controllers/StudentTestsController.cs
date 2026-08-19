using System.Security.Claims;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;
using MathInsight.Modules.TestGen.Queries.GetTopicPracticeOptions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.TestGen.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/test-generator/tests")]
public sealed class StudentTestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudentTestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("blueprint-options")]
    public async Task<IActionResult> GetBlueprintOptions(CancellationToken cancellationToken)
    {
        var studentId = GetCurrentStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetBlueprintExamOptionsQuery(studentId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpGet("topic-practice-options")]
    public async Task<IActionResult> GetTopicPracticeOptions(CancellationToken cancellationToken)
    {
        var studentId = GetCurrentStudentId();
        if (studentId is null) return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));
        var result = await _mediator.Send(new GetTopicPracticeOptionsQuery(studentId), cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("blueprint-exams")]
    public async Task<IActionResult> GenerateBlueprintExam(
        [FromBody] GenerateBlueprintExamRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BlueprintId))
            return BadRequest(new ApiErrorResponse(TestGenerationErrors.RequestInvalid));

        var studentId = GetCurrentStudentId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GenerateBlueprintExamCommand(request.BlueprintId, studentId),
            cancellationToken);

        return result.IsFailure
            ? ToErrorResult(result.Error!)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("topic-practices")]
    public async Task<IActionResult> GenerateTopicPractice([FromBody] GenerateTopicPracticeRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TagId)) return BadRequest(new ApiErrorResponse(TestGenerationErrors.RequestInvalid));
        var studentId = GetCurrentStudentId();
        if (studentId is null) return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));
        var difficultyId = string.IsNullOrWhiteSpace(request.DifficultyId) ? null : request.DifficultyId.Trim();
        var result = await _mediator.Send(new GenerateTopicPracticeCommand(studentId, request.TagId.Trim(), difficultyId), cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private string? GetCurrentStudentId()
        => User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private IActionResult ToErrorResult(Error error)
    {
        if (error == ApplicationErrors.AuthInvalidToken)
            return Unauthorized(new ApiErrorResponse(error));

        if (error == TestGenerationErrors.StudentNotFound ||
            error == TestGenerationErrors.BlueprintNotFound ||
            error == TestGenerationErrors.TopicPracticeStudentNotFound ||
            error == TestGenerationErrors.TopicPracticeTopicNotFound ||
            error == TestGenerationErrors.TopicPracticeDifficultyNotFound)
        {
            return NotFound(new ApiErrorResponse(error));
        }

        if (error == TestGenerationErrors.BlueprintUnavailable ||
            error == TestGenerationErrors.GradeMismatch ||
            error == TestGenerationErrors.TopicPracticeTopicUnavailable ||
            error == TestGenerationErrors.StudentGradeRequired ||
            error == TestGenerationErrors.TopicPracticeGradeNotAllowed ||
            error == TestGenerationErrors.TopicParentGradeMismatch ||
            error == TestGenerationErrors.TopicParentNotAssignable ||
            error == TestGenerationErrors.TopicPracticeDifficultyUnavailable)
        {
            return UnprocessableEntity(new ApiErrorResponse(error));
        }

        if (error == TestGenerationErrors.InsufficientQuestions ||
            error == TestGenerationErrors.TopicPracticeInsufficientQuestions ||
            error == TestGenerationErrors.TopicPracticeGenerationConflict)
            return Conflict(new ApiErrorResponse(error));

        if (error == TestGenerationErrors.TopicPracticeRecommenderUnavailable ||
            error == TestGenerationErrors.TopicPracticeRecommendationInvalid ||
            error == TestGenerationErrors.AdaptiveExamMasteryUnavailable ||
            error == TestGenerationErrors.AdaptiveExamMasteryInvalid)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorResponse(error));
        }

        return BadRequest(new ApiErrorResponse(error));
    }
}

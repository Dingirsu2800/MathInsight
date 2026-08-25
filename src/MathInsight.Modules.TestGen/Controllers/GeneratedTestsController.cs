using System.Security.Claims;
using MathInsight.Modules.TestGen.Commands.ArchiveSharedBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Queries.GetExpertTestPreview;
using MathInsight.Modules.TestGen.Queries.GetSharedBlueprintExams;
using MathInsight.Modules.TestGen.Queries.ResolveSharedTestCode;
using MathInsight.Modules.TestGen.RateLimiting;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathInsight.Modules.TestGen.Controllers;

[ApiController]
[Route("api/test-generator/tests")]
public sealed class GeneratedTestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeneratedTestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Authorize(Roles = "Student")]
    [HttpGet("shared-blueprint-exams")]
    public async Task<IActionResult> GetSharedBlueprintExams(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        [FromQuery] string? generationType,
        CancellationToken cancellationToken)
    {
        var studentId = GetCurrentAccountId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetSharedBlueprintExamsQuery(studentId, pageIndex, pageSize, generationType),
            cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [Authorize(Roles = "Student")]
    [EnableRateLimiting(TestCodeResolutionRateLimit.PolicyName)]
    [HttpPost("resolve-code")]
    public async Task<IActionResult> ResolveTestCode(
        [FromBody] ResolveTestCodeRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TestCode))
            return BadRequest(new ApiErrorResponse(TestGenerationErrors.RequestInvalid));

        var studentId = GetCurrentAccountId();
        if (studentId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new ResolveSharedTestCodeQuery(studentId, request.TestCode),
            cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpGet("{testId}/expert-preview")]
    public async Task<IActionResult> GetExpertPreview(
        string testId,
        CancellationToken cancellationToken)
    {
        var expertId = GetCurrentAccountId();
        if (expertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetExpertTestPreviewQuery(testId, expertId),
            cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpPatch("{testId}/status")]
    public async Task<IActionResult> UpdateStatus(
        string testId,
        [FromBody] UpdateGeneratedTestStatusRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(TestGenerationErrors.RequestInvalid));

        var expertId = GetCurrentAccountId();
        if (expertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new ArchiveSharedBlueprintExamCommand(testId, expertId, request.Status),
            cancellationToken);
        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    private string? GetCurrentAccountId()
        => User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private IActionResult ToErrorResult(Error error)
    {
        if (error == ApplicationErrors.AuthInvalidToken)
            return Unauthorized(new ApiErrorResponse(error));
        if (error == BlueprintErrors.MutationForbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));
        if (error == TestGenerationErrors.StudentNotFound ||
            error == TestGenerationErrors.TestCodeNotAvailable ||
            error == TestGenerationErrors.GeneratedTestNotFound)
        {
            return NotFound(new ApiErrorResponse(error));
        }
        if (error == TestGenerationErrors.QuestionVersionMissing)
            return UnprocessableEntity(new ApiErrorResponse(error));
        if (error == TestGenerationErrors.TestContainsInvalidatedQuestion)
            return Conflict(new ApiErrorResponse(error));

        return BadRequest(new ApiErrorResponse(error));
    }
}

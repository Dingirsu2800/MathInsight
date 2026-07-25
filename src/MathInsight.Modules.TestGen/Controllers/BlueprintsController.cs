using System.Security.Claims;
using MathInsight.Modules.TestGen.Commands.CloneBlueprint;
using MathInsight.Modules.TestGen.Commands.CreateBlueprint;
using MathInsight.Modules.TestGen.Commands.DeleteBlueprint;
using MathInsight.Modules.TestGen.Commands.ReviewBlueprint;
using MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;
using MathInsight.Modules.TestGen.Commands.UpdateBlueprint;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Queries.GetBlueprintDetail;
using MathInsight.Modules.TestGen.Queries.GetBlueprintList;
using MathInsight.Modules.TestGen.Queries.GetPendingBlueprints;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.TestGen.Controllers;

[ApiController]
[Authorize(Roles = "Expert")]
[Route("api/test-generator/blueprints")]
public sealed class BlueprintsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BlueprintsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetBlueprints(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        [FromQuery] string? status,
        [FromQuery] int? grade,
        [FromQuery] string? expertId,
        [FromQuery] string? search,
        [FromQuery] bool includeDeactivated = false,
        CancellationToken cancellationToken = default)
    {
        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetBlueprintListQuery(
                pageIndex,
                pageSize,
                status,
                grade,
                expertId,
                search,
                includeDeactivated,
                currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingBlueprints(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetPendingBlueprintsQuery(pageIndex, pageSize, currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpGet("{blueprintId}")]
    public async Task<IActionResult> GetBlueprintDetail(
        string blueprintId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetBlueprintDetailQuery(blueprintId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBlueprint(
        [FromBody] BlueprintRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(BlueprintErrors.RequestInvalid));

        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new CreateBlueprintCommand(request, currentExpertId),
            cancellationToken);

        return result.IsFailure
            ? ToErrorResult(result.Error!)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{blueprintId}")]
    public async Task<IActionResult> UpdateBlueprint(
        string blueprintId,
        [FromBody] BlueprintRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(BlueprintErrors.RequestInvalid));

        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new UpdateBlueprintCommand(blueprintId, request, currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{blueprintId}/submit")]
    public async Task<IActionResult> SubmitBlueprintForReview(
        string blueprintId,
        CancellationToken cancellationToken)
    {
        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new SubmitBlueprintForReviewCommand(blueprintId, currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{blueprintId}/review")]
    public async Task<IActionResult> ReviewBlueprint(
        string blueprintId,
        [FromBody] ReviewBlueprintRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(BlueprintErrors.RequestInvalid));

        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new ReviewBlueprintCommand(blueprintId, request, currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{blueprintId}/clone")]
    public async Task<IActionResult> CloneBlueprint(
        string blueprintId,
        CancellationToken cancellationToken)
    {
        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new CloneBlueprintCommand(blueprintId, currentExpertId),
            cancellationToken);

        return result.IsFailure
            ? ToErrorResult(result.Error!)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpDelete("{blueprintId}")]
    public async Task<IActionResult> DeleteBlueprint(
        string blueprintId,
        CancellationToken cancellationToken)
    {
        var currentExpertId = GetCurrentExpertId();
        if (currentExpertId is null)
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new DeleteBlueprintCommand(blueprintId, currentExpertId),
            cancellationToken);

        return result.IsFailure ? ToErrorResult(result.Error!) : Ok(result.Value);
    }

    private string? GetCurrentExpertId()
        => User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private IActionResult ToErrorResult(Error error)
    {
        if (error == ApplicationErrors.AuthInvalidToken)
            return Unauthorized(new ApiErrorResponse(error));

        if (error == BlueprintErrors.NotFound)
            return NotFound(new ApiErrorResponse(error));

        if (error == BlueprintErrors.MutationForbidden ||
            error == BlueprintErrors.SelfReviewForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));
        }

        if (error == BlueprintErrors.StatusInvalid ||
            error == BlueprintErrors.StructureInvalid ||
            error == BlueprintErrors.TotalMismatch)
        {
            return UnprocessableEntity(new ApiErrorResponse(error));
        }

        if (error == BlueprintErrors.InUse)
            return Conflict(new ApiErrorResponse(error));

        return BadRequest(new ApiErrorResponse(error));
    }
}

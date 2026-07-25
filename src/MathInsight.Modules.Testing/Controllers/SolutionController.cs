using System.Security.Claims;
using MathInsight.Modules.Testing.Queries.GetDetailedSolution;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Testing.Controllers;

/// <summary>
/// Provides access to detailed grading solutions for completed test sessions.
/// Only accessible to Students once a session reaches Graded status (UC-50).
/// </summary>
[ApiController]
[Authorize(Roles = "Student")]
[Route("api/v1/tests/sessions")]
public class SolutionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SolutionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string? GetStudentId() =>
        User.FindFirst("account_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// UC-50: Returns the detailed solution for a graded test session.
    /// Returns 403 if the session has not yet reached Graded status (DC-04).
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with full question/answer/explanation data, or 403/404 on error.</returns>
    [HttpGet("{id}/solution")]
    public async Task<IActionResult> GetDetailedSolution(
        string id,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var query = new GetDetailedSolutionQuery(id, studentId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            // Session exists but is not yet Graded — 403 per spec
            if (result.Error.Code == "TESTING_SESSION_NOT_GRADED")
                return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        return Ok(result.Value);
    }
}

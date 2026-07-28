using System.Security.Claims;
using MathInsight.Modules.Gamification.Queries.GetStreak;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Gamification.Controllers;

/// <summary>
/// REST endpoints for the Gamification module. Student-scoped; the caller operates only on their
/// own data, resolved from the access token. Auto-discovered by the host's AddControllers(),
/// exactly like RecommenderController — no explicit application-part registration needed.
/// </summary>
[ApiController]
[Route("api/v1/gamification")]
[Authorize(Roles = "Student")]
public class GamificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public GamificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// UC-81: the authenticated student's study streak (current + longest, and whether it is
    /// still active today).
    /// </summary>
    [HttpGet("streak")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStreak(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        var result = await _mediator.Send(new GetStreakQuery(studentId), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error?.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Extracts the authenticated student's ID from JWT claims. Mirrors RecommenderController:
    /// TokenService writes the account id to both "account_id" and NameIdentifier, and a
    /// Student's AccountID is its StudentID, so NameIdentifier is the student id here.
    /// </summary>
    private string? GetAuthenticatedStudentId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

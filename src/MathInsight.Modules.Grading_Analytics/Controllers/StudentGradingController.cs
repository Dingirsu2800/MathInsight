using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;

namespace MathInsight.Modules.Grading_Analytics.Controllers;

/// <summary>
/// Student-facing query endpoints for the Grading module.
/// UC-55: GET /api/v1/grading/sessions/{sessionId}    — view session result
/// UC-56: GET /api/v1/grading/student/history         — paginated session history
/// UC-56: GET /api/v1/grading/student/stats           — aggregate stats
/// All endpoints require an authenticated Student role.
/// </summary>
[ApiController]
[Route("api/v1/grading")]
[Authorize(Roles = "Student")]
public class StudentGradingController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudentGradingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// UC-55: Returns the full result of a graded (or in-progress) session.
    /// Only the session owner may access their own session.
    /// </summary>
    /// <param name="sessionId">The test session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>SessionResultDto or 403/404.</returns>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(SessionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionResult(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        try
        {
            var result = await _mediator.Send(
                new GetSessionResultQuery(sessionId, studentId),
                cancellationToken);

            if (result is null)
                return NotFound(new { error = $"Session {sessionId} not found." });

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You do not have access to this session." });
        }
    }

    /// <summary>
    /// UC-56: Returns a paginated list of the authenticated student's graded sessions.
    /// Ordered by submission date descending (BR-UC56-02).
    /// </summary>
    [HttpGet("student/history")]
    [ProducesResponseType(typeof(PagedResult<SessionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? testFormat = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        var result = await _mediator.Send(
            new GetSessionHistoryQuery(
                studentId,
                page,
                pageSize,
                testFormat,
                fromDate,
                toDate),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// UC-56: Returns aggregate statistics for the authenticated student's history.
    /// Includes total sessions, sessions in last 30 days, average score, and accuracy percent.
    /// </summary>
    [HttpGet("student/stats")]
    [ProducesResponseType(typeof(StudentHistoryStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        var result = await _mediator.Send(
            new GetStudentHistoryStatsQuery(studentId),
            cancellationToken);

        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? GetAuthenticatedStudentId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(claim) ? null : claim;
    }
}

using System.Security.Claims;
using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.RecordIncident;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Commands.ReportSessionQuestion;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Testing.Controllers;

/// <summary>
/// Manages student test sessions: start, auto-save, incident tracking, and submission.
/// All endpoints require the caller to be authenticated as a Student (UC-47, UC-48, UC-49).
/// </summary>
[ApiController]
[Authorize(Roles = "Student")]
[Route("api/v1/tests/sessions")]
public class TestSessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TestSessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string? GetStudentId() =>
        User.FindFirst("account_id")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// UC-47: Creates a new InProgress test session for the authenticated student.
    /// </summary>
    /// <param name="request">Request body containing the TestId to start.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with session details including question list, or 400/409 on error.</returns>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartSessionRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var command = new StartSessionCommand(request.TestId, studentId);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_ALREADY_IN_PROGRESS")
                return Conflict(new ApiErrorResponse(result.Error));

            if (result.Error.Code == "TESTING_TEST_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        return Created($"api/v1/tests/sessions/{result.Value!.SessionId}", result.Value);
    }

    /// <summary>
    /// UC-47: Auto-saves the student's current answers for the session.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="request">Batch of answer updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with savedAt timestamp and remainingSeconds.</returns>
    [HttpPost("{id}/auto-save")]
    public async Task<IActionResult> AutoSave(
        string id,
        [FromBody] AutoSaveRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var command = new AutoSaveCommand(id, studentId, request.Answers);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            if (result.Error.Code == "TESTING_SESSION_NOT_IN_PROGRESS")
                return Conflict(new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-47: Records a proctoring incident (TAB_SWITCH or FOCUS_LOSS) for the session.
    /// If the total incident count reaches 5, the session is force-submitted (BR-10).
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="request">Incident type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with incident details and forceSubmitted flag.</returns>
    [HttpPost("{id}/incident")]
    public async Task<IActionResult> RecordIncident(
        string id,
        [FromBody] RecordIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var command = new RecordIncidentCommand(id, studentId, request.Type);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            if (result.Error.Code == "TESTING_SESSION_NOT_IN_PROGRESS")
                return Conflict(new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-49: Submits the test session normally (student-initiated).
    /// Practice mode: returns 200 with graded result.
    /// Exam mode: returns 202 Accepted (grading is asynchronous).
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 (Practice, Graded) or 202 (Exam, queued) or 409 if already submitted.</returns>
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitSession(
        string id,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var command = new SubmitSessionCommand(id, studentId);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            if (result.Error.Code is "TESTING_SESSION_NOT_IN_PROGRESS"
                                  or "TESTING_SESSION_ALREADY_COMPLETED")
                return Conflict(new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        // Practice mode → 200 OK with graded result
        // Exam mode → 202 Accepted (handler sets SubmissionType accordingly)
        if (result.Value!.SubmissionType == "StudentSubmit" && result.Value.Status == "Graded")
            return Ok(result.Value);

        return Accepted(result.Value);
    }

    /// <summary>
    /// UC-48: Reports a question during an active test session.
    /// Delegates to QuestionBank module's report flow with session context for audit.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="qId">The question ID to report.</param>
    /// <param name="request">Report reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 on success.</returns>
    [HttpPost("{id}/questions/{qId}/report")]
    public async Task<IActionResult> ReportSessionQuestion(
        string id,
        string qId,
        [FromBody] ReportSessionQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized();

        var command = new ReportSessionQuestionCommand(id, qId, studentId, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Code == "TESTING_SESSION_NOT_FOUND")
                return NotFound(new ApiErrorResponse(result.Error));

            return BadRequest(new ApiErrorResponse(result.Error));
        }

        return Ok();
    }
}

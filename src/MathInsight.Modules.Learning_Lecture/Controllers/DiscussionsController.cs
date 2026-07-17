using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Commands.Discussions;
using MathInsight.Modules.Learning_Lecture.Queries.Discussions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Learning_Lecture.Controllers;

[ApiController]
[Route("api/v1/discussions")]
[Authorize]
public class DiscussionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DiscussionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private bool IsStudent => User.FindFirst(ClaimTypes.Role)?.Value == "Student";
    private bool IsTeacherOrAdmin => User.FindFirst(ClaimTypes.Role)?.Value == "Teacher" || User.FindFirst(ClaimTypes.Role)?.Value == "Admin";

    [HttpGet("lectures/{lectureId}")]
    public async Task<IActionResult> GetDiscussions(string lectureId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDiscussionsQuery(lectureId, IsStudent, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("moderation-queue")]
    public async Task<IActionResult> GetModerationQueue([FromQuery] string? teacherId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (!IsTeacherOrAdmin) return Forbid();
        
        var isTeacher = User.FindFirst(ClaimTypes.Role)?.Value == "Teacher";
        if (isTeacher)
        {
            teacherId = CurrentUserId;
        }

        var result = await _mediator.Send(new GetModerationQueueQuery(teacherId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("questions")]
    public async Task<IActionResult> AskQuestion([FromBody] AskQuestionRequest request, CancellationToken cancellationToken)
    {
        if (!IsStudent) return Forbid();
        try
        {
            var result = await _mediator.Send(new AskDiscussionQuestionCommand(request.LectureId, CurrentUserId, request.Title, request.Content), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("questions/{questionId}/answers")]
    public async Task<IActionResult> AnswerQuestion(string questionId, [FromBody] AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new AnswerDiscussionQuestionCommand(questionId, CurrentUserId, request.Content), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("comments/{id}")]
    public async Task<IActionResult> UpdateComment(string id, [FromBody] UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new UpdateDiscussionCommentCommand(id, request.IsQuestion, CurrentUserId, request.Content, IsTeacherOrAdmin), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("comments/{id}")]
    public async Task<IActionResult> DeleteComment(string id, [FromQuery] bool isQuestion, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteDiscussionCommentCommand(id, isQuestion, CurrentUserId, IsTeacherOrAdmin), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("comments/{id}/hide")]
    public async Task<IActionResult> HideComment(string id, [FromBody] HideCommentRequest request, CancellationToken cancellationToken)
    {
        if (!IsTeacherOrAdmin) return Forbid();
        try
        {
            await _mediator.Send(new HideDiscussionCommentCommand(id, request.IsQuestion, CurrentUserId), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reports")]
    public async Task<IActionResult> ReportDiscussion([FromBody] ReportDiscussionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new ReportDiscussionCommand(request.QuestionId, request.AnswerId, CurrentUserId, request.Reason), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reports/{reportId}/resolve")]
    public async Task<IActionResult> ResolveReport(string reportId, [FromBody] ResolveReportRequest request, CancellationToken cancellationToken)
    {
        if (!IsTeacherOrAdmin) return Forbid();
        try
        {
            await _mediator.Send(new ResolveModerationCommand(reportId, CurrentUserId, request.IsDismissed), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record AskQuestionRequest(string LectureId, string Title, string Content);
public record AnswerQuestionRequest(string Content);
public record UpdateCommentRequest(bool IsQuestion, string Content);
public record HideCommentRequest(bool IsQuestion);
public record ReportDiscussionRequest(string? QuestionId, string? AnswerId, string Reason);
public record ResolveReportRequest(bool IsDismissed);

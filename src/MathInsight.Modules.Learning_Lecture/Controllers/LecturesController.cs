using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Commands.Lectures;
using MathInsight.Modules.Learning_Lecture.Queries.Lectures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Learning_Lecture.Controllers;

[ApiController]
[Route("api/v1/lectures")]
[Authorize]
public class LecturesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LecturesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private bool IsStudent => User.FindFirst(ClaimTypes.Role)?.Value == "Student";
    private bool IsAdmin => User.FindFirst(ClaimTypes.Role)?.Value == "Admin";

    [HttpGet]
    public async Task<IActionResult> GetLectures([FromQuery] string? teacherId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = new GetLectureListQuery(teacherId, IsStudent, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLecture(string id, CancellationToken cancellationToken)
    {
        var studentId = IsStudent ? CurrentUserId : null;
        var query = new GetLectureQuery(id, studentId);
        try
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateLecture([FromBody] CreateLectureRequest request, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        var cmd = new CreateLectureCommand(request.Title, request.Content, request.VideoUrl, request.ThumbnailUrl, request.TagId, CurrentUserId);
        var result = await _mediator.Send(cmd, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLecture(string id, [FromBody] UpdateLectureRequest request, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        var cmd = new UpdateLectureCommand(id, request.Title, request.Content, request.VideoUrl, request.ThumbnailUrl, request.TagId, CurrentUserId);
        try
        {
            var result = await _mediator.Send(cmd, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishLecture(string id, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new PublishLectureCommand(id, CurrentUserId, IsAdmin), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivateLecture(string id, CancellationToken cancellationToken)
    {
        if (IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new DeactivateLectureCommand(id, CurrentUserId, IsAdmin), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/like")]
    public async Task<IActionResult> LikeLecture(string id, CancellationToken cancellationToken)
    {
        if (!IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new LikeLectureCommand(id, CurrentUserId), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}/like")]
    public async Task<IActionResult> UnlikeLecture(string id, CancellationToken cancellationToken)
    {
        if (!IsStudent) return Forbid();
        try
        {
            await _mediator.Send(new UnlikeLectureCommand(id, CurrentUserId), cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record CreateLectureRequest(string Title, string? Content, string? VideoUrl, string? ThumbnailUrl, string TagId);
public record UpdateLectureRequest(string Title, string? Content, string? VideoUrl, string? ThumbnailUrl, string TagId);

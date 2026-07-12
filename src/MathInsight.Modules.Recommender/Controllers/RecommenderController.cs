using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using MathInsight.Modules.Recommender.Queries.GetWeakTags;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;

namespace MathInsight.Modules.Recommender.Controllers;

/// <summary>
/// REST endpoints for the Recommender module (UC-52, UC-53, UC-54).
/// All endpoints are restricted to authenticated Students only (G2).
/// No Redis, Python, SAR, Hangfire, or separate service required for MVP.
/// </summary>
[ApiController]
[Route("api/v1/recommender")]
[Authorize(Roles = "Student")]
public class RecommenderController : ControllerBase
{
    private readonly IMediator _mediator;

    public RecommenderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// UC-52: Returns the authenticated student's weak topics
    /// (topics where OfficialPoint &lt; 5.00).
    /// </summary>
    [HttpGet("weak-tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWeakTags(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        var result = await _mediator.Send(
            new GetWeakTagsQuery(studentId.Value), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// UC-53: Returns recommended lectures based on the student's weak tags (RCM-10).
    /// Matches Lecture.TagID to weak TagIDs; remedial topics sorted first.
    /// </summary>
    [HttpGet("lectures")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendedLectures(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        var result = await _mediator.Send(
            new GetRecommendedLecturesQuery(studentId.Value), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// UC-54: Returns recommended materials based on the student's weak tags (RCM-10).
    /// Matches materials through LectureMaterial join table; remedial topics sorted first.
    /// </summary>
    [HttpGet("materials")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendedMaterials(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (studentId is null)
            return Unauthorized(new { error = "Invalid or missing student identity." });

        var result = await _mediator.Send(
            new GetRecommendedMaterialsQuery(studentId.Value), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Extracts the authenticated student's ID from JWT claims.
    /// Returns null if the claim is missing or not a valid GUID.
    /// </summary>
    private Guid? GetAuthenticatedStudentId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var studentId))
            return null;

        return studentId;
    }
}

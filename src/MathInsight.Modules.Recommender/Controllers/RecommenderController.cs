using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediatR;
using MathInsight.Modules.Recommender.Queries.GetWeakTags;
using MathInsight.Modules.Recommender.Queries.GetAllTagsMastery;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;
using MathInsight.Modules.Recommender.Errors;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Recommender.Controllers;

/// <summary>
/// REST endpoints for the Recommender module (UC-52, UC-53, UC-54, UC-55).
/// All endpoints are restricted to authenticated Students only (G2).
/// No Redis, Python, SAR, Hangfire, or separate service required for MVP.
/// </summary>
[ApiController]
[Route("api/v1/recommender")]
[Authorize(Roles = "Student")]
public class RecommenderController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RecommenderController> _logger;

    public RecommenderController(IMediator mediator, ILogger<RecommenderController> logger)
    {
        _mediator = mediator;
        _logger = logger;
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
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetWeakTagsQuery(studentId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// UC-55: Returns ALL topic mastery rows for the authenticated student (RCM-17).
    /// Unlike weak-tags, this endpoint includes NotLearned, Learning, and Mastered topics.
    /// Used by the Competency page to show a complete picture of the student's performance.
    /// </summary>
    [HttpGet("topic-mastery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllTagsMastery(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetAllTagsMasteryQuery(studentId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// UC-53: Returns difficulty-aware lectures for qualified mastery contexts,
    /// or grade foundation lectures when the student has no qualified evidence.
    /// </summary>
    [HttpGet("lectures")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendedLectures(CancellationToken cancellationToken)
    {
        var studentId = GetAuthenticatedStudentId();
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        try
        {
            var result = await _mediator.Send(
                new GetRecommendedLecturesQuery(studentId), cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to produce lecture recommendations for student {StudentId}", studentId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(RecommenderErrors.LectureRecommendationUnavailable));
        }
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
        if (string.IsNullOrWhiteSpace(studentId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetRecommendedMaterialsQuery(studentId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Extracts the authenticated student's ID from JWT claims.
    /// Returns null if the claim is missing.
    /// </summary>
    private string? GetAuthenticatedStudentId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

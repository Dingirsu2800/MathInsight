using System.Security.Claims;
using MathInsight.Modules.Grading_Analytics.Controllers;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// TC-INT-StudentGradingController-001..007
/// Integration tests for StudentGradingController (UC-55, UC-56).
/// Tests HTTP status code mapping for GetSessionResult, GetHistory, GetStats.
/// Uses Mock&lt;IMediator&gt; — no WebApplicationFactory required.
/// </summary>
public sealed class StudentGradingControllerTests
{
    private const string StudentId = "student-sga-001";
    private const string SessionId = "session-sga-001";

    // ── Helper ──────────────────────────────────────────────────────────────

    private static StudentGradingController CreateController(
        IMediator mediator,
        bool withStudentClaim = true)
    {
        var claims = withStudentClaim
            ? new[] { new Claim(ClaimTypes.NameIdentifier, StudentId) }
            : Array.Empty<Claim>();

        var controller = new StudentGradingController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
        return controller;
    }

    private static SessionResultDto MakeSessionResultDto() => new()
    {
        SessionId      = SessionId,
        TestFormat     = "Practice",
        Status         = "Graded",
        Score          = 8.0m,
        NumCorrect     = 4,
        NumIncorrect   = 1,
        NumAbandoned   = 0,
        TotalQuestion  = 5,
        GradeRevision  = 1,
        Answers        = []
    };

    // ── TC-INT-StudentGradingController-001 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-001: UC-55 — Owner views own session.
    /// Handler returns SessionResultDto → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetSessionResult_Owner_Returns200WithDto()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetSessionResultQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSessionResultDto());

        var controller = CreateController(mediator.Object);

        var result = await controller.GetSessionResult(SessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SessionResultDto>(ok.Value);
        Assert.Equal(SessionId, dto.SessionId);
        Assert.Equal(8.0m, dto.Score);
    }

    // ── TC-INT-StudentGradingController-002 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-002: UC-55 — Session not found.
    /// Handler returns null → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetSessionResult_NotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetSessionResultQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionResultDto?)null);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetSessionResult("nonexistent", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── TC-INT-StudentGradingController-003 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-003: UC-55 — Non-owner access.
    /// Handler throws UnauthorizedAccessException → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task GetSessionResult_NonOwner_Returns403()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetSessionResultQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not owner"));

        var controller = CreateController(mediator.Object);

        var result = await controller.GetSessionResult(SessionId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    // ── TC-INT-StudentGradingController-004 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-004: UC-55 — Missing student identity.
    /// No NameIdentifier claim → 401 Unauthorized; handler never called.
    /// </summary>
    [Fact]
    public async Task GetSessionResult_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetSessionResult(SessionId, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetSessionResultQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-INT-StudentGradingController-005 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-005: UC-56 — GetHistory returns paged list.
    /// Handler returns PagedResult → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetHistory_AuthenticatedStudent_Returns200WithPagedResult()
    {
        var pagedResult = new PagedResult<SessionHistoryDto>
        {
            Items      = [new SessionHistoryDto { SessionId = SessionId, Status = "Graded" }],
            TotalCount = 1,
            Page       = 1,
            PageSize   = 20
        };

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetSessionHistoryQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetHistory(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<SessionHistoryDto>>(ok.Value);
        Assert.Equal(1, paged.TotalCount);
        Assert.Single(paged.Items);
    }

    // ── TC-INT-StudentGradingController-006 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-006: UC-56 — GetHistory without auth.
    /// Missing NameIdentifier claim → 401 Unauthorized; handler never called.
    /// </summary>
    [Fact]
    public async Task GetHistory_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetHistory(cancellationToken: CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetSessionHistoryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-INT-StudentGradingController-007 ──────────────────────────────────

    /// <summary>
    /// TC-INT-StudentGradingController-007: UC-56 — GetStats returns aggregate stats.
    /// Handler returns StudentHistoryStatsDto → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetStats_AuthenticatedStudent_Returns200WithStats()
    {
        var stats = new StudentHistoryStatsDto
        {
            TotalSessions       = 10,
            SessionsLast30Days  = 3,
            AverageScore        = 7.5m,
            AccuracyPercent     = 72.0m
        };

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetStudentHistoryStatsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetStats(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<StudentHistoryStatsDto>(ok.Value);
        Assert.Equal(10, dto.TotalSessions);
        Assert.Equal(7.5m, dto.AverageScore);
    }
}

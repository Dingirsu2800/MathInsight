using System.Security.Claims;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Controllers;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Queries.GetDetailedSolution;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// TC-INT-SolutionController-001..004
/// Integration tests for SolutionController (GET /api/v1/tests/sessions/{id}/solution, UC-50).
/// Tests HTTP status mapping: 200 on success, 403 when not graded (DC-04), 404 not found, 401 no auth.
/// Uses Mock&lt;IMediator&gt; — no WebApplicationFactory required.
/// </summary>
public sealed class SolutionControllerTests
{
    private const string SessionId = "session-sol-001";
    // TestDataSeeder.StudentId is a readonly field, not const — assign via static field
    private static readonly string StudentId = TestDataSeeder.StudentId;

    // ── Helper ───────────────────────────────────────────────────────────────

    private static SolutionController CreateController(
        IMediator mediator,
        bool withStudentClaim = true)
    {
        var claims = withStudentClaim
            ? new[] { new Claim("account_id", StudentId) }
            : Array.Empty<Claim>();

        return new SolutionController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private static DetailedSolutionResponse MakeSolutionResponse() =>
        new(
            SessionId:    SessionId,
            TestName:     "Practice Test 1",
            Score:        8.0m,
            NumCorrect:   4,
            NumIncorrect: 1,
            NumAbandoned: 0,
            Questions:    []);

    // ── TC-INT-SolutionController-001 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-SolutionController-001: UC-50 — Session is Graded.
    /// Handler returns DetailedSolutionResponse → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetDetailedSolution_GradedSession_Returns200WithSolution()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.Is<GetDetailedSolutionQuery>(q => q.SessionId == SessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DetailedSolutionResponse>.Success(MakeSolutionResponse()));

        var controller = CreateController(mediator.Object);

        var result = await controller.GetDetailedSolution(SessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DetailedSolutionResponse>(ok.Value);
        Assert.Equal(SessionId, response.SessionId);
        Assert.Equal(8.0m, response.Score);
    }

    // ── TC-INT-SolutionController-002 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-SolutionController-002: UC-50 / DC-04 — Session not yet Graded (InProgress).
    /// Handler returns Failure(SessionNotGraded) → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task GetDetailedSolution_NotGradedSession_Returns403()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetDetailedSolutionQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DetailedSolutionResponse>.Failure(TestingErrors.SessionNotGraded));

        var controller = CreateController(mediator.Object);

        var result = await controller.GetDetailedSolution(SessionId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        var error = Assert.IsType<ApiErrorResponse>(statusResult.Value);
        Assert.Equal("TESTING_SESSION_NOT_GRADED", error.Code);
    }

    // ── TC-INT-SolutionController-003 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-SolutionController-003: UC-50 — Session does not exist.
    /// Handler returns Failure(SessionNotFound) → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetDetailedSolution_SessionNotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetDetailedSolutionQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DetailedSolutionResponse>.Failure(TestingErrors.SessionNotFound));

        var controller = CreateController(mediator.Object);

        var result = await controller.GetDetailedSolution("nonexistent-session", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(((NotFoundObjectResult)result).Value!);
        Assert.Equal("TESTING_SESSION_NOT_FOUND", error.Code);
    }

    // ── TC-INT-SolutionController-004 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-SolutionController-004: UC-50 — Missing student identity claim.
    /// No account_id claim → 401 Unauthorized; handler never called.
    /// </summary>
    [Fact]
    public async Task GetDetailedSolution_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetDetailedSolution(SessionId, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetDetailedSolutionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

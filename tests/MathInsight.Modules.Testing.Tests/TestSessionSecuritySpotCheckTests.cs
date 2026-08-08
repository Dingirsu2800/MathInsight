using System.Security.Claims;
using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Controllers;
using MathInsight.Modules.Testing.Queries.GetDetailedSolution;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// TC-SYS-Sec-001..004
/// Security spot-check tests for Testing module controllers.
/// Verifies that all endpoints return 401 Unauthorized when the student identity
/// claim is absent — without mocking auth middleware (controller-unit level).
/// </summary>
public sealed class TestSessionSecuritySpotCheckTests
{
    // ── Helper: controller with NO identity claim ──────────────────────────

    private static TestSessionsController CreateUnauthenticatedSessionsController(IMediator mediator)
        => new(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // No claims at all — simulates missing/invalid JWT
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

    private static SolutionController CreateUnauthenticatedSolutionController(IMediator mediator)
        => new(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

    // ── TC-SYS-Sec-001 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Sec-001: POST /api/v1/tests/sessions — No student claim → 401.
    /// IMediator.Send must NOT be called.
    /// </summary>
    [Fact]
    public async Task StartSession_WithoutClaim_Returns401_AndHandlerNotCalled()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateUnauthenticatedSessionsController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<StartSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-SYS-Sec-002 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Sec-002: POST /api/v1/tests/sessions/{id}/auto-save — No claim → 401.
    /// </summary>
    [Fact]
    public async Task AutoSave_WithoutClaim_Returns401_AndHandlerNotCalled()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateUnauthenticatedSessionsController(mediator.Object);

        var result = await controller.AutoSave(
            "any-session",
            new AutoSaveRequest(Answers: []),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<AutoSaveCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-SYS-Sec-003 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Sec-003: POST /api/v1/tests/sessions/{id}/submit — No claim → 401.
    /// </summary>
    [Fact]
    public async Task SubmitSession_WithoutClaim_Returns401_AndHandlerNotCalled()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateUnauthenticatedSessionsController(mediator.Object);

        var result = await controller.SubmitSession("any-session", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<SubmitSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-SYS-Sec-004 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Sec-004: GET /api/v1/tests/sessions/{id}/solution — No claim → 401.
    /// </summary>
    [Fact]
    public async Task GetDetailedSolution_WithoutClaim_Returns401_AndHandlerNotCalled()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateUnauthenticatedSolutionController(mediator.Object);

        var result = await controller.GetDetailedSolution("any-session", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetDetailedSolutionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

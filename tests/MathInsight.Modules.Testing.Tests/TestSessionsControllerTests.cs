using System.Security.Claims;
using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.RecordIncident;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Controllers;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Queries.GetInProgressSession;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// TC-INT-TestSessionsController-001..015
/// Integration tests for TestSessionsController.
/// All tests use Mock&lt;IMediator&gt; — no WebApplicationFactory required.
/// </summary>
public sealed class TestSessionsControllerTests
{
    // ── TC-INT-TestSessionsController-001 (existing) ─────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-001: UC-47/BR-15 — Duplicate session.
    /// StartSession returns Failure(SessionAlreadyInProgress); GetInProgressSession returns existing id
    /// → 409 Conflict with SessionAlreadyInProgressResponse containing existing session id.
    /// </summary>
    [Fact]
    public async Task StartSession_Duplicate_ReturnsExistingSessionId()
    {
        const string existingSessionId = "existing-session";
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.IsAny<StartSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Failure(TestingErrors.SessionAlreadyInProgress));
        mediator
            .Setup(item => item.Send(
                It.IsAny<GetInProgressSessionQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(existingSessionId));
        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<SessionAlreadyInProgressResponse>(conflict.Value);
        Assert.Equal("TESTING_SESSION_ALREADY_IN_PROGRESS", response.Code);
        Assert.Equal(existingSessionId, response.ExistingSessionId);
    }

    // ── TC-INT-TestSessionsController-002 (existing) ─────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-002: UC-47/BR-15 — Access denied.
    /// StartSession returns Failure(TestAccessDenied) → 403 Forbidden with stable error code.
    /// </summary>
    [Fact]
    public async Task StartSession_AccessDenied_Returns403WithStableCode()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.IsAny<StartSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Failure(TestingErrors.TestAccessDenied));

        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var error = Assert.IsType<ApiErrorResponse>(forbidden.Value);
        Assert.Equal("TESTING_TEST_ACCESS_DENIED", error.Code);
    }

    [Fact]
    public async Task StartSession_InvalidatedQuestion_Returns409WithStableCode()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.IsAny<StartSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Failure(TestingErrors.TestContainsInvalidatedQuestion));
        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(TestingErrors.TestContainsInvalidatedQuestion.Code, error.Code);
    }

    // ── TC-INT-TestSessionsController-003 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-003: UC-47 — Happy path StartSession.
    /// Handler returns Success(StartSessionResponse) → 201 Created.
    /// </summary>
    [Fact]
    public async Task StartSession_HappyPath_Returns201Created()
    {
        var sessionResponse = new StartSessionResponse(
            SessionId: "session-new-001",
            TestId: TestDataSeeder.ActiveTestId,
            TestFormat: "Practice",
            Status: "InProgress",
            StartTime: DateTime.UtcNow,
            DurationMinutes: 60,
            TotalQuestions: 5,
            Questions: [],
            HasTimeLimit: true,
            RemainingSeconds: 3600,
            ElapsedSeconds: 0);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<StartSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Success(sessionResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<StartSessionResponse>(created.Value);
        Assert.Equal("InProgress", response.Status);
        Assert.Equal(5, response.TotalQuestions);
    }

    // ── TC-INT-TestSessionsController-004 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-004: UC-47 — AutoSave happy path.
    /// Handler returns Success(AutoSaveResponse) → 200 OK with savedAt timestamp.
    /// </summary>
    [Fact]
    public async Task AutoSave_HappyPath_Returns200WithTimestamp()
    {
        var savedAt = DateTime.UtcNow;
        var autoSaveResponse = new AutoSaveResponse(
            SavedAt: savedAt,
            HasTimeLimit: true,
            RemainingSeconds: 3000,
            ElapsedSeconds: 600);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AutoSaveResponse>.Success(autoSaveResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.AutoSave(
            "session-001",
            new AutoSaveRequest(Answers: []),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AutoSaveResponse>(ok.Value);
        Assert.Equal(savedAt, response.SavedAt);
    }

    // ── TC-INT-TestSessionsController-005 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-005: UC-47 — AutoSave on expired session.
    /// Handler returns Failure(SessionExpired) → 409 Conflict with error code.
    /// </summary>
    [Fact]
    public async Task AutoSave_SessionExpired_Returns409Conflict()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AutoSaveResponse>.Failure(TestingErrors.SessionExpired));

        var controller = CreateController(mediator.Object);

        var result = await controller.AutoSave(
            "session-expired",
            new AutoSaveRequest(Answers: []),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal("TESTING_SESSION_EXPIRED", error.Code);
    }

    // ── TC-INT-TestSessionsController-006 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-006: UC-47 — AutoSave session not found.
    /// Handler returns Failure(SessionNotFound) → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task AutoSave_SessionNotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AutoSaveResponse>.Failure(TestingErrors.SessionNotFound));

        var controller = CreateController(mediator.Object);

        var result = await controller.AutoSave(
            "nonexistent",
            new AutoSaveRequest(Answers: []),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── TC-INT-TestSessionsController-007 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-007: UC-47/BR-10 — RecordIncident, not yet force-submitted.
    /// Handler returns Success(RecordIncidentResponse, ForceSubmitted=false) → 200 OK.
    /// </summary>
    [Fact]
    public async Task RecordIncident_NotForceSubmitted_Returns200WithFlag()
    {
        var incidentResponse = new RecordIncidentResponse(
            IncidentId: "incident-001",
            TotalIncidents: 4,
            ForceSubmitted: false);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RecordIncidentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecordIncidentResponse>.Success(incidentResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.RecordIncident(
            "session-001",
            new RecordIncidentRequest("TAB_SWITCH"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RecordIncidentResponse>(ok.Value);
        Assert.Equal(4, response.TotalIncidents);
        Assert.False(response.ForceSubmitted);
    }

    // ── TC-INT-TestSessionsController-008 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-008: UC-47/BR-10 — 5th incident → ForceSubmitted=true.
    /// Handler returns Success(RecordIncidentResponse, ForceSubmitted=true) → 200 OK.
    /// </summary>
    [Fact]
    public async Task RecordIncident_FifthIncident_Returns200WithForceSubmittedTrue()
    {
        var incidentResponse = new RecordIncidentResponse(
            IncidentId: "incident-005",
            TotalIncidents: 5,
            ForceSubmitted: true);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RecordIncidentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecordIncidentResponse>.Success(incidentResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.RecordIncident(
            "session-001",
            new RecordIncidentRequest("FOCUS_LOSS"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RecordIncidentResponse>(ok.Value);
        Assert.Equal(5, response.TotalIncidents);
        Assert.True(response.ForceSubmitted);
    }

    // ── TC-INT-TestSessionsController-009 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-009: UC-47 — RecordIncident session not found.
    /// Handler returns Failure(SessionNotFound) → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task RecordIncident_SessionNotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RecordIncidentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RecordIncidentResponse>.Failure(TestingErrors.SessionNotFound));

        var controller = CreateController(mediator.Object);

        var result = await controller.RecordIncident(
            "nonexistent",
            new RecordIncidentRequest("TAB_SWITCH"),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── TC-INT-TestSessionsController-010 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-010: UC-49 — SubmitSession Practice mode.
    /// Handler returns Success with Status=Graded, SubmissionType=StudentSubmit → 200 OK.
    /// </summary>
    [Fact]
    public async Task SubmitSession_PracticeGraded_Returns200Ok()
    {
        var submitResponse = new SubmitSessionResponse(
            SessionId: "session-001",
            Status: "Graded",
            SubmissionType: "StudentSubmit",
            NumAbandoned: 0,
            Score: 8.0m);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Success(submitResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.SubmitSession("session-001", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubmitSessionResponse>(ok.Value);
        Assert.Equal("Graded", response.Status);
        Assert.Equal("StudentSubmit", response.SubmissionType);
    }

    // ── TC-INT-TestSessionsController-011 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-011: UC-49 — SubmitSession Exam mode.
    /// Handler returns Success with Status=Graded, SubmissionType=StudentSubmit but grading queued
    /// — controller logic: if NOT (StudentSubmit AND Graded) → 202 Accepted.
    /// </summary>
    [Fact]
    public async Task SubmitSession_ExamQueued_Returns202Accepted()
    {
        // Exam: queued for async grading → Status is not yet "Graded"
        var submitResponse = new SubmitSessionResponse(
            SessionId: "session-exam-001",
            Status: "Submitted",
            SubmissionType: "StudentSubmit",
            NumAbandoned: 0,
            Score: null);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Success(submitResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.SubmitSession("session-exam-001", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<SubmitSessionResponse>(accepted.Value);
        Assert.Equal("StudentSubmit", response.SubmissionType);
    }

    // ── TC-INT-TestSessionsController-012 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-012: UC-49/DC-03 — Submit already completed session.
    /// Handler returns Failure(SessionAlreadyCompleted) → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task SubmitSession_AlreadyCompleted_Returns409Conflict()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Failure(TestingErrors.SessionAlreadyCompleted));

        var controller = CreateController(mediator.Object);

        var result = await controller.SubmitSession("session-graded", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal("TESTING_SESSION_ALREADY_COMPLETED", error.Code);
    }

    // ── TC-INT-TestSessionsController-013 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-013: UC-49 — SubmitSession, session not found.
    /// Handler returns Failure(SessionNotFound) → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task SubmitSession_SessionNotFound_Returns404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotFound));

        var controller = CreateController(mediator.Object);

        var result = await controller.SubmitSession("nonexistent", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── TC-INT-TestSessionsController-014 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-014: — TimeoutSubmit on non-expired session.
    /// Handler returns Failure(SessionNotExpired) → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task TimeoutSubmitSession_NotExpired_Returns409Conflict()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TimeoutSubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotExpired));

        var controller = CreateController(mediator.Object);

        var result = await controller.TimeoutSubmitSession("session-active", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal("TESTING_SESSION_NOT_EXPIRED", error.Code);
    }

    // ── TC-INT-TestSessionsController-015 ────────────────────────────────────

    /// <summary>
    /// TC-INT-TestSessionsController-015: — TimeoutSubmit on expired Practice session.
    /// Handler returns Success with Status=Graded → 200 OK.
    /// </summary>
    [Fact]
    public async Task TimeoutSubmitSession_ExpiredPractice_Returns200WithGraded()
    {
        var submitResponse = new SubmitSessionResponse(
            SessionId: "session-expired",
            Status: "Graded",
            SubmissionType: "TimeoutSubmit",
            NumAbandoned: 3,
            Score: 4.0m);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<TimeoutSubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Success(submitResponse));

        var controller = CreateController(mediator.Object);

        var result = await controller.TimeoutSubmitSession("session-expired", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubmitSessionResponse>(ok.Value);
        Assert.Equal("Graded", response.Status);
        Assert.Equal("TimeoutSubmit", response.SubmissionType);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static TestSessionsController CreateController(IMediator mediator)
        => new(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("account_id", TestDataSeeder.StudentId)],
                        "Test"))
                }
            }
        };
}


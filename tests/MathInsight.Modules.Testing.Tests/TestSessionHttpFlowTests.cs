using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Commands.RecordIncident;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Queries.GetDetailedSolution;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// TC-SYS-Flow-001..005
/// System-level flow tests for Testing module.
/// Exercises multi-step lifecycle flows through handler layer with InMemory EF.
/// Uses the same direct-handler pattern as TestSessionIntegrationTests.
/// </summary>
public sealed class TestSessionHttpFlowTests
{
    // ── TC-SYS-Flow-001 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Flow-001: UC-47, UC-49 — Full Practice session lifecycle.
    /// Start → AutoSave → Submit → Graded.
    /// Verifies session transitions InProgress → Graded.
    /// </summary>
    [Fact]
    public async Task Flow_StartAutoSaveSubmit_SessionBecomesGraded()
    {
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var gradingMediator = CreateGradingMediator(db);

        // Act 1: Start
        var startResult = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            default);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Act 2: AutoSave one answer
        var saveResult = await new AutoSaveCommandHandler(db).Handle(
            new AutoSaveCommand(sessionId, TestDataSeeder.StudentId,
            [
                new AutoSaveAnswerDto(
                    QuestionId:      TestDataSeeder.Question1Id,
                    AnswerId:        TestDataSeeder.Answer1Id,
                    ShortAnswerText: null,
                    TimeSpent:       30,
                    SelectedOptions: null,
                    Parts:           null)
            ]),
            default);
        Assert.True(saveResult.IsSuccess);

        // Act 3: Submit
        var submitResult = await new SubmitSessionCommandHandler(db, gradingMediator).Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            default);
        Assert.True(submitResult.IsSuccess);

        // Assert
        Assert.Equal("Graded", submitResult.Value!.Status);
        Assert.Equal("StudentSubmit", submitResult.Value.SubmissionType);

        var dbSession = await db.TestSessions.SingleAsync(s => s.SessionId == sessionId);
        Assert.Equal("Graded", dbSession.Status);
        Assert.NotNull(dbSession.EndTime);
    }

    // ── TC-SYS-Flow-002 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Flow-002: UC-47/BR-10 — ForceSubmit on 5th incident.
    /// Start → record 5 incidents → session auto-submitted with SystemSubmit.
    /// </summary>
    [Fact]
    public async Task Flow_FiveIncidents_SessionForceSubmitted()
    {
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedSharedBlueprintExam(db);

        var gradingMediator = CreateGradingMediator(db);

        // Start exam session
        var startResult = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.SharedTestId, TestDataSeeder.StudentId),
            default);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Incidents 1-4
        var incidentHandler = new RecordIncidentCommandHandler(db, gradingMediator);

        for (int i = 1; i <= 4; i++)
        {
            var resp = await incidentHandler.Handle(
                new RecordIncidentCommand(sessionId, TestDataSeeder.StudentId, "TAB_SWITCH"),
                default);
            Assert.True(resp.IsSuccess);
            Assert.False(resp.Value!.ForceSubmitted, $"Incident {i} should not force-submit");
        }

        // 5th → ForceSubmit
        var fifthResult = await incidentHandler.Handle(
            new RecordIncidentCommand(sessionId, TestDataSeeder.StudentId, "FOCUS_LOSS"),
            default);

        Assert.True(fifthResult.IsSuccess);
        Assert.True(fifthResult.Value!.ForceSubmitted);
        Assert.Equal(5, fifthResult.Value.TotalIncidents);

        var dbSession = await db.TestSessions.SingleAsync(s => s.SessionId == sessionId);
        Assert.Equal("Graded", dbSession.Status);
        Assert.Equal("SystemSubmit", dbSession.SubmissionType);
    }

    // ── TC-SYS-Flow-003 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Flow-003: UC-47, UC-49 — Session expires then TimeoutSubmit.
    /// Start → manipulate StartTime to simulate expiry → TimeoutSubmitSession → TimeoutSubmit.
    /// </summary>
    [Fact]
    public async Task Flow_SessionExpires_TimeoutSubmitSucceeds()
    {
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var mediator = new Mock<IMediator>();

        // Start
        var startResult = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            default);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Simulate expiry: set StartTime 61 minutes in the past
        var session = await db.TestSessions.SingleAsync(s => s.SessionId == sessionId);
        session.StartTime = DateTime.UtcNow.AddMinutes(-61);
        await db.SaveChangesAsync();
        var gradingMediator = CreateGradingMediator(db);

        // TimeoutSubmit
        var timeoutResult = await new TimeoutSubmitSessionCommandHandler(db, gradingMediator).Handle(
            new TimeoutSubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            default);

        Assert.True(timeoutResult.IsSuccess);
        Assert.Equal("TimeoutSubmit", timeoutResult.Value!.SubmissionType);
    }

    // ── TC-SYS-Flow-004 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Flow-004: UC-50/DC-04 — Access to solution before session is graded.
    /// Start → GetDetailedSolution (InProgress) → TESTING_SESSION_NOT_GRADED error.
    /// </summary>
    [Fact]
    public async Task Flow_GetSolutionBeforeSubmit_ReturnsAccessDenied()
    {
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var startResult = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            default);
        var sessionId = startResult.Value!.SessionId;

        var solutionResult = await new GetDetailedSolutionQueryHandler(db).Handle(
            new GetDetailedSolutionQuery(sessionId, TestDataSeeder.StudentId),
            default);

        Assert.True(solutionResult.IsFailure);
        Assert.Equal("TESTING_SESSION_NOT_GRADED", solutionResult.Error!.Code);
    }

    // ── TC-SYS-Flow-005 ───────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-Flow-005: UC-49, UC-50 — Solution accessible after graded.
    /// Start → Submit → GetDetailedSolution → full data returned.
    /// </summary>
    [Fact]
    public async Task Flow_SubmitThenGetSolution_ReturnsSolutionData()
    {
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start
        var startResult = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            default);
        var sessionId = startResult.Value!.SessionId;
        var gradingMediator = CreateGradingMediator(db);

        // Submit
        var submitResult = await new SubmitSessionCommandHandler(db, gradingMediator).Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            default);
        Assert.True(submitResult.IsSuccess);
        Assert.Equal("Graded", submitResult.Value!.Status);

        // GetSolution
        var solutionResult = await new GetDetailedSolutionQueryHandler(db).Handle(
            new GetDetailedSolutionQuery(sessionId, TestDataSeeder.StudentId),
            default);

        Assert.True(solutionResult.IsSuccess);
        Assert.Equal(sessionId, solutionResult.Value!.SessionId);
        Assert.Equal(5, solutionResult.Value.Questions.Count);
    }

    // ── Helper: simulates Practice grading via mediator publish ──────────────

    /// <summary>
    /// Creates a mock IMediator that, when TestSubmittedEvent is published,
    /// simulates grading by setting Status="Graded" on the session.
    /// Same pattern as TestSessionIntegrationTests.CreateGradingMediator.
    /// </summary>
    private static IMediator CreateGradingMediator(Persistence.TestingDbContext db)
    {
        var mock = new Mock<IMediator>();

        mock
            .Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(async (INotification notification, CancellationToken ct) =>
            {
                if (notification is TestSubmittedEvent evt)
                {
                    var allSessions = await db.TestSessions.ToListAsync(ct);
                    var session = allSessions.FirstOrDefault(s => s.SessionId == evt.SessionId);

                    if (session is not null && session.Status == "InProgress")
                    {
                        session.Status         = "Graded";
                        session.SubmissionType = evt.SubmissionType;
                        session.EndTime        = evt.SubmittedTime;
                        session.NumCorrect     = 2;
                        session.NumIncorrect   = 1;
                        session.Score          = 6.67m;
                        await db.SaveChangesAsync(ct);
                    }
                }
            });

        // Also handle ForceSubmitSessionCommand for incident-based force-submit
        mock
            .Setup(m => m.Send(It.IsAny<ForceSubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (ForceSubmitSessionCommand cmd, CancellationToken ct) =>
            {
                var forceHandler = new MathInsight.Modules.Testing.Commands.ForceSubmitSession
                    .ForceSubmitSessionCommandHandler(db, mock.Object);
                return await forceHandler.Handle(cmd, ct);
            });

        return mock.Object;
    }
}

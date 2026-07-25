using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Commands.RecordIncident;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Queries.GetDetailedSolution;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// Integration tests for the Testing module (003) covering UC-47, UC-49, UC-50.
/// Tests exercise command/query handlers against EF Core InMemory provider with
/// a mocked IMediator for grading event publishing.
/// </summary>
public class TestSessionIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // UC-47: Start session → InProgress, correct question count
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_ActiveTest_ReturnsInProgressWithCorrectQuestionCount()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var handler = new StartSessionCommandHandler(db);
        var command = new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal("InProgress", response.Status);
        Assert.Equal(TestDataSeeder.ActiveTestId, response.TestId);
        Assert.Equal("Practice", response.TestFormat);
        Assert.Equal(5, response.TotalQuestions);
        Assert.Equal(5, response.Questions.Count);

        // Verify TestAnswer stubs were created
        var answers = await db.TestAnswers.ToListAsync();
        var sessionAnswers = answers.Where(a => a.SessionId == response.SessionId).ToList();
        Assert.Equal(5, sessionAnswers.Count);

        // Verify session exists in DB
        var sessions = await db.TestSessions.ToListAsync();
        var session = sessions.First(s => s.SessionId == response.SessionId);
        Assert.Equal("InProgress", session.Status);
        Assert.Null(session.SubmissionType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-47: Start duplicate InProgress session → 409 (BR-15)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_DuplicateInProgress_ReturnsSessionAlreadyInProgressError()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var handler = new StartSessionCommandHandler(db);
        var command = new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);

        // Start first session successfully
        var firstResult = await handler.Handle(command, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        // Act — try to start a second session for the same student + test
        var secondResult = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(secondResult.IsFailure);
        Assert.Equal("TESTING_SESSION_ALREADY_IN_PROGRESS", secondResult.Error!.Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-47: Auto-save 5 answers → persisted, update_choice_time set
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoSave_FiveAnswers_PersistedAndUpdateChoiceTimeSet()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start a session first
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Prepare 5 answer updates
        var answerDtos = new List<AutoSaveAnswerDto>
        {
            new(QuestionId: TestDataSeeder.Question1Id, AnswerId: TestDataSeeder.Answer1Id,
                ShortAnswerText: null, TimeSpent: 30, SelectedOptions: null, Parts: null),
            new(QuestionId: TestDataSeeder.Question2Id, AnswerId: null,
                ShortAnswerText: "42", TimeSpent: 45, SelectedOptions: null, Parts: null),
            new(QuestionId: TestDataSeeder.Question3Id, AnswerId: null,
                ShortAnswerText: null, TimeSpent: 20,
                SelectedOptions: new List<AutoSaveOptionDto>
                {
                    new("opt-a"), new("opt-b")
                }, Parts: null),
            new(QuestionId: TestDataSeeder.Question4Id, AnswerId: null,
                ShortAnswerText: null, TimeSpent: 15, SelectedOptions: null,
                Parts: new List<AutoSavePartDto>
                {
                    new("part-1", BooleanAnswer: true, TextAnswer: null, NumericAnswer: null),
                    new("part-2", BooleanAnswer: null, TextAnswer: "answer text", NumericAnswer: null)
                }),
            new(QuestionId: TestDataSeeder.Question5Id, AnswerId: "ans-5",
                ShortAnswerText: null, TimeSpent: 10, SelectedOptions: null, Parts: null)
        };

        var autoSaveHandler = new AutoSaveCommandHandler(db);
        var autoSaveCommand = new AutoSaveCommand(sessionId, TestDataSeeder.StudentId, answerDtos);

        // Act
        var result = await autoSaveHandler.Handle(autoSaveCommand, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RemainingSeconds > 0);
        Assert.True(result.Value.SavedAt <= DateTime.UtcNow);

        // Verify all 5 answers have UpdateChoiceTime set (use client-side evaluation for InMemory)
        var allAnswers = await db.TestAnswers.ToListAsync();
        var savedAnswers = allAnswers.Where(a => a.SessionId == sessionId).ToList();

        Assert.Equal(5, savedAnswers.Count);
        foreach (var ans in savedAnswers)
        {
            Assert.NotNull(ans.UpdateChoiceTime);
            Assert.NotNull(ans.FirstChoiceTime);
        }

        // Verify specific answer fields
        var q1Answer = savedAnswers.First(a => a.QuestionId == TestDataSeeder.Question1Id);
        Assert.Equal(TestDataSeeder.Answer1Id, q1Answer.AnswerId);
        Assert.Equal(30, q1Answer.TimeSpent);

        var q2Answer = savedAnswers.First(a => a.QuestionId == TestDataSeeder.Question2Id);
        Assert.Equal("42", q2Answer.ShortAnswerText);

        // Verify options for Q3
        var q3AnswerId = savedAnswers.First(a => a.QuestionId == TestDataSeeder.Question3Id).TestAnswerId;
        var allOptions = await db.TestAnswerOptions.ToListAsync();
        var q3Options = allOptions.Where(o => o.TestAnswerId == q3AnswerId).ToList();
        Assert.Equal(2, q3Options.Count);

        // Verify parts for Q4
        var q4AnswerId = savedAnswers.First(a => a.QuestionId == TestDataSeeder.Question4Id).TestAnswerId;
        var allParts = await db.TestAnswerParts.ToListAsync();
        var q4Parts = allParts.Where(p => p.TestAnswerId == q4AnswerId).ToList();
        Assert.Equal(2, q4Parts.Count);
        Assert.Contains(q4Parts, p => p.BooleanAnswer == true);
        Assert.Contains(q4Parts, p => p.TextAnswer == "answer text");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-47: 4 incidents → no force-submit; 5th → Graded + SystemSubmit
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordIncident_FourIncidents_NoForceSubmit_FifthTriggersForceSubmit()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Create a mock IMediator for RecordIncidentHandler:
        // When it sends ForceSubmitSessionCommand, use a real ForceSubmitSessionCommandHandler
        // with a grading-simulating mediator.
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<ForceSubmitSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (ForceSubmitSessionCommand cmd, CancellationToken ct) =>
            {
                var gradingMock = CreateGradingMediator(db);
                var forceHandler = new ForceSubmitSessionCommandHandler(db, gradingMock);
                return await forceHandler.Handle(cmd, ct);
            });

        var incidentHandler = new RecordIncidentCommandHandler(db, mediatorMock.Object);

        // Act — log 4 incidents, none should trigger force-submit
        for (int i = 1; i <= 4; i++)
        {
            var incidentResult = await incidentHandler.Handle(
                new RecordIncidentCommand(sessionId, TestDataSeeder.StudentId, "TAB_SWITCH"),
                CancellationToken.None);
            Assert.True(incidentResult.IsSuccess);
            Assert.False(incidentResult.Value!.ForceSubmitted);
            Assert.Equal(i, incidentResult.Value.TotalIncidents);
        }

        // Verify session is still InProgress after 4 incidents
        var sessionAfter4 = await db.TestSessions.FindAsync(sessionId);
        Assert.NotNull(sessionAfter4);
        Assert.Equal("InProgress", sessionAfter4.Status);

        // Act — 5th incident triggers force-submit (BR-10)
        var fifthResult = await incidentHandler.Handle(
            new RecordIncidentCommand(sessionId, TestDataSeeder.StudentId, "FOCUS_LOSS"),
            CancellationToken.None);

        // Assert
        Assert.True(fifthResult.IsSuccess);
        Assert.True(fifthResult.Value!.ForceSubmitted);
        Assert.Equal(5, fifthResult.Value.TotalIncidents);

        // Verify the session was force-submitted and graded
        var sessionAfter5 = await db.TestSessions.FindAsync(sessionId);
        Assert.NotNull(sessionAfter5);
        Assert.Equal("Graded", sessionAfter5.Status);
        Assert.Equal("SystemSubmit", sessionAfter5.SubmissionType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-49: Normal submit → Graded, StudentSubmit, grading fields populated
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitSession_NormalSubmit_ReturnsGradedWithStudentSubmit()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start session
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Answer some questions via auto-save
        var autoSaveHandler = new AutoSaveCommandHandler(db);
        await autoSaveHandler.Handle(
            new AutoSaveCommand(sessionId, TestDataSeeder.StudentId, new List<AutoSaveAnswerDto>
            {
                new(TestDataSeeder.Question1Id, TestDataSeeder.Answer1Id, null, 10, null, null),
                new(TestDataSeeder.Question2Id, "ans-2", null, 15, null, null),
            }),
            CancellationToken.None);

        // Use a mediator mock that simulates Practice grading (sets status to Graded)
        var gradingMediator = CreateGradingMediator(db);
        var submitHandler = new SubmitSessionCommandHandler(db, gradingMediator);

        // Act
        var result = await submitHandler.Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(sessionId, response.SessionId);
        Assert.Equal("Graded", response.Status);
        Assert.Equal("StudentSubmit", response.SubmissionType);

        // Verify session in DB
        var session = await db.TestSessions.FindAsync(sessionId);
        Assert.NotNull(session);
        Assert.Equal("Graded", session.Status);
        Assert.Equal("StudentSubmit", session.SubmissionType);
        Assert.NotNull(session.EndTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-49: Submit Graded session → 409 (DC-03)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitSession_AlreadyGraded_ReturnsSessionAlreadyCompleted()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start and submit session to Graded
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        var gradingMediator = CreateGradingMediator(db);
        var submitHandler = new SubmitSessionCommandHandler(db, gradingMediator);

        // First submit succeeds
        var firstSubmit = await submitHandler.Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(firstSubmit.IsSuccess);
        Assert.Equal("Graded", firstSubmit.Value!.Status);

        // Act — try to submit again
        var secondSubmit = await submitHandler.Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        // Assert — should fail with DC-03
        Assert.True(secondSubmit.IsFailure);
        Assert.Equal("TESTING_SESSION_ALREADY_COMPLETED", secondSubmit.Error!.Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-49: Submit with unanswered questions → NumAbandoned = unanswered count
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitSession_WithUnansweredQuestions_NumAbandonedEqualsUnansweredCount()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start session (5 questions get stubs)
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Answer only 2 of 5 questions — 3 should be abandoned
        var autoSaveHandler = new AutoSaveCommandHandler(db);
        await autoSaveHandler.Handle(
            new AutoSaveCommand(sessionId, TestDataSeeder.StudentId, new List<AutoSaveAnswerDto>
            {
                new(TestDataSeeder.Question1Id, TestDataSeeder.Answer1Id, null, 10, null, null),
                new(TestDataSeeder.Question3Id, null, "Short answer text", 20, null, null),
            }),
            CancellationToken.None);

        var gradingMediator = CreateGradingMediator(db);
        var submitHandler = new SubmitSessionCommandHandler(db, gradingMediator);

        // Act
        var result = await submitHandler.Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.NumAbandoned); // Q2, Q4, Q5 are unanswered
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-50: View solution before Graded → 403
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailedSolution_BeforeGraded_ReturnsSessionNotGraded()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start session but do NOT submit
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        var solutionHandler = new GetDetailedSolutionQueryHandler(db);

        // Act
        var result = await solutionHandler.Handle(
            new GetDetailedSolutionQuery(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        // Assert — should fail with "not graded" error (maps to 403 in controller)
        Assert.True(result.IsFailure);
        Assert.Equal("TESTING_SESSION_NOT_GRADED", result.Error!.Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UC-50: View solution after Graded → full data returned
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailedSolution_AfterGraded_ReturnsFullQuestionAnswerData()
    {
        // Arrange
        await using var ctx = TestingInMemoryContext.Create();
        var db = ctx.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        // Start session
        var startHandler = new StartSessionCommandHandler(db);
        var startResult = await startHandler.Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        var sessionId = startResult.Value!.SessionId;

        // Answer a question
        var autoSaveHandler = new AutoSaveCommandHandler(db);
        await autoSaveHandler.Handle(
            new AutoSaveCommand(sessionId, TestDataSeeder.StudentId, new List<AutoSaveAnswerDto>
            {
                new(TestDataSeeder.Question1Id, TestDataSeeder.Answer1Id, null, 10, null, null),
            }),
            CancellationToken.None);

        // Submit → Graded
        var gradingMediator = CreateGradingMediator(db);
        var submitHandler = new SubmitSessionCommandHandler(db, gradingMediator);
        var submitResult = await submitHandler.Handle(
            new SubmitSessionCommand(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);
        Assert.True(submitResult.IsSuccess);
        Assert.Equal("Graded", submitResult.Value!.Status);

        var solutionHandler = new GetDetailedSolutionQueryHandler(db);

        // Act
        var result = await solutionHandler.Handle(
            new GetDetailedSolutionQuery(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var solution = result.Value!;
        Assert.Equal(sessionId, solution.SessionId);
        Assert.Equal("Practice Test 1", solution.TestName);
        Assert.Equal(5, solution.Questions.Count);

        // Verify question data contains answers
        var q1 = solution.Questions.First(q => q.QuestionId == TestDataSeeder.Question1Id);
        Assert.Equal(TestDataSeeder.Answer1Id, q1.SelectedAnswerId);
        Assert.Equal(1, q1.QuestionNo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: Creates a mock IMediator that simulates Practice grading
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a mock IMediator that, when a TestSubmittedEvent is published,
    /// simulates grading by setting the session Status to Graded and populating score fields.
    /// This avoids depending on the actual Grading module (004).
    ///
    /// Key design: The mock intercepts Publish(INotification, CancellationToken) and
    /// directly mutates the session entity in the shared DbContext, mimicking what the
    /// real Grading module handler would do.
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
                    var sessionIdStr = evt.SessionId;
                    var allSessions = await db.TestSessions.ToListAsync(ct);
                    var session = allSessions.FirstOrDefault(s => s.SessionId == sessionIdStr);

                    if (session is not null && session.Status == "InProgress")
                    {
                        // Simulate grading: set status and score
                        session.Status = "Graded";
                        session.SubmissionType = evt.SubmissionType;
                        session.EndTime = evt.SubmittedTime;
                        session.NumCorrect = 2;
                        session.NumIncorrect = 1;
                        session.Score = 6.67m;
                        await db.SaveChangesAsync(ct);
                    }
                }
            });

        return mock.Object;
    }
}

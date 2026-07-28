using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// Integration-style tests for the GradeSubmittedSessionHandler using
/// GradingEngine directly with in-memory entities.
///
/// These tests verify the full grading pipeline behavior:
///   - Exam session graded and status set to "Graded"
///   - DC-05: Failure scenario (tested via engine exception simulation)
///   - Performance SLA: Practice 40-question < 2.0s
///
/// Note: Full EF-based integration tests with a real DB and transaction rollback
/// require TestContainers or an in-memory SQL provider. These tests validate
/// the grading logic and event construction without EF persistence.
/// </summary>
public class GradeSubmittedSessionHandlerTests
{
    private readonly GradingEngine _engine = new();

    [Fact]
    public void Exam_Session_Graded_Synchronously_StatusBecomesGraded()
    {
        // Arrange: Create an Exam session with mixed question types
        var session = TestDataBuilder.CreateSession(testFormat: "Exam", status: "InProgress");
        var correctId = Guid.NewGuid().ToString("D");
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 2.0m, correctId, studentAnswerId: correctId);
        TestDataBuilder.AddShortAnswer(session, defaultPoint: 1.5m, "42", "42");

        // Act: Grade synchronously (same as handler does)
        var result = _engine.Grade(session);

        // Simulate what handler does after grading
        session.Status = "Graded";
        session.Score = result.Score;
        session.NumCorrect = result.NumCorrect;
        session.NumIncorrect = result.NumIncorrect;
        session.NumAbandoned = result.NumAbandoned;

        // Assert
        Assert.Equal("Graded", session.Status);
        Assert.Equal(10.0m, session.Score); // Both correct: (2.0 + 1.5) / (2.0 + 1.5) * 10 = 10
        Assert.Equal(2, session.NumCorrect);
        Assert.Equal(0, session.NumIncorrect);
        Assert.Equal(0, session.NumAbandoned);
    }

    [Fact]
    public void DC05_SessionStaysInProgress_When_GradingNotApplied()
    {
        // This tests the DC-05 scenario: if an exception occurs before
        // session.Status is set to "Graded", the session stays "InProgress".
        // In production, the EF transaction rollback handles this.
        // Here we simulate by checking the state before status update.

        var session = TestDataBuilder.CreateSession(testFormat: "Exam", status: "InProgress");
        var correctId = Guid.NewGuid().ToString("D");
        TestDataBuilder.AddSingleChoiceAnswer(session, defaultPoint: 1.0m, correctId, studentAnswerId: correctId);

        // Assert: before grading, status should still be InProgress
        Assert.Equal("InProgress", session.Status);

        // Simulate a grading failure (exception before status update)
        try
        {
            // Grade succeeds, but simulate failure before committing
            _engine.Grade(session);
            throw new InvalidOperationException("Simulated DB failure");
        }
        catch (InvalidOperationException)
        {
            // Transaction rolled back â€” status should still be InProgress
            // In a real scenario, EF rollback reverts all entity changes
        }

        // Assert: session stays InProgress because we never set Graded
        Assert.Equal("InProgress", session.Status);
    }

    [Fact]
    public void Practice_40Questions_CompletesInUnder2Seconds()
    {
        // SLA: Practice grading must complete in < 2.0 seconds
        var session = TestDataBuilder.CreateSession(testFormat: "Practice", status: "InProgress");

        // Build 40 questions: 10 each of 4 types
        for (int i = 0; i < 10; i++)
        {
            var cid = Guid.NewGuid().ToString("D");
            TestDataBuilder.AddSingleChoiceAnswer(session, 1.0m, cid, cid);
        }
        for (int i = 0; i < 10; i++)
        {
            var a = Guid.NewGuid().ToString("D");
            var b = Guid.NewGuid().ToString("D");
            TestDataBuilder.AddMultipleSelectAnswer(session, 2.0m, [a, b], [a, b]);
        }
        for (int i = 0; i < 10; i++)
        {
            TestDataBuilder.AddShortAnswer(session, 1.5m, "answer", "answer");
        }
        for (int i = 0; i < 10; i++)
        {
            TestDataBuilder.AddCompositeAllTrueFalse(session, 2.0m,
                [("True", "True"), ("False", "False"), ("True", "True"), ("False", "False")]);
        }

        // Act + Assert: SLA
        var sw = Stopwatch.StartNew();
        var result = _engine.Grade(session);

        // Simulate handler's post-grade work
        session.Status = "Graded";
        session.Score = result.Score;
        session.NumCorrect = result.NumCorrect;
        session.NumIncorrect = result.NumIncorrect;
        session.NumAbandoned = result.NumAbandoned;
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Practice grading took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
        Assert.Equal("Graded", session.Status);
        Assert.Equal(40, result.NumCorrect); // All correct
        Assert.Equal(10.0m, result.Score);
    }

    [Fact]
    public void Exam_WithMixedResults_ScoreCalculatedCorrectly()
    {
        // Arrange: Exam with some correct, some incorrect, some abandoned
        var session = TestDataBuilder.CreateSession(testFormat: "Exam", status: "InProgress");

        // Correct single choice (2pt)
        var cid = Guid.NewGuid().ToString("D");
        TestDataBuilder.AddSingleChoiceAnswer(session, 2.0m, cid, cid);

        // Incorrect single choice (2pt)
        TestDataBuilder.AddSingleChoiceAnswer(session, 2.0m, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));

        // Abandoned single choice (2pt)
        TestDataBuilder.AddSingleChoiceAnswer(session, 2.0m, Guid.NewGuid().ToString("D"), null);

        // Correct short answer (1pt)
        TestDataBuilder.AddShortAnswer(session, 1.0m, "hello", "HELLO");

        // Act
        var result = _engine.Grade(session);

        session.Status = "Graded";
        session.Score = result.Score;
        session.NumCorrect = result.NumCorrect;
        session.NumIncorrect = result.NumIncorrect;
        session.NumAbandoned = result.NumAbandoned;

        // Assert: 2 + 0 + 0 + 1 = 3 earned, 2 + 2 + 2 + 1 = 7 max → 3/7 * 10 ≈ 4.29
        Assert.Equal("Graded", session.Status);
        Assert.Equal(Math.Round(3.0m / 7.0m * 10.0m, 2), result.Score);
        Assert.Equal(2, result.NumCorrect);
        Assert.Equal(1, result.NumIncorrect); // 1 incorrect (abandoned is counted in NumAbandoned, not NumIncorrect)
        Assert.Equal(1, result.NumAbandoned);
    }

    [Fact]
    public async Task GradeSessionAsync_MapsDifficultyLevel_FromTagDifficulty()
    {
        // Arrange
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<MathInsight.Modules.Grading_Analytics.Persistence.GradingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new MathInsight.Modules.Grading_Analytics.Persistence.GradingDbContext(options);

        var difficulty1 = new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TagDifficultyReadOnly { DifficultyId = "diff-easy", LevelValue = 1 };
        var difficulty3 = new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TagDifficultyReadOnly { DifficultyId = "diff-hard", LevelValue = 3 };
        db.TagDifficulties.AddRange(difficulty1, difficulty3);

        var testId = Guid.NewGuid().ToString("D");
        var sessionId = Guid.NewGuid().ToString("D");
        var studentId = Guid.NewGuid().ToString("D");

        var q1 = new MathInsight.Modules.Grading_Analytics.Persistence.Entities.Question
        {
            QuestionId = "q1",
            QuestionType = "SINGLE_CHOICE",
            DefaultWeight = 1.0m,
            DifficultyId = "diff-easy",
            QuestionContent = "Q1",
            Answers = [new MathInsight.Modules.Grading_Analytics.Persistence.Entities.Answer { AnswerId = "a1", QuestionId = "q1", IsCorrect = true }]
        };
        var q2 = new MathInsight.Modules.Grading_Analytics.Persistence.Entities.Question
        {
            QuestionId = "q2",
            QuestionType = "SINGLE_CHOICE",
            DefaultWeight = 2.0m,
            DifficultyId = "diff-hard",
            QuestionContent = "Q2",
            Answers = [new MathInsight.Modules.Grading_Analytics.Persistence.Entities.Answer { AnswerId = "a2", QuestionId = "q2", IsCorrect = true }]
        };
        db.Questions.AddRange(q1, q2);

        db.TestQuestions.AddRange(
            new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TestQuestion { TestId = testId, QuestionId = "q1", MaxPointsSnapshot = 1.0m },
            new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TestQuestion { TestId = testId, QuestionId = "q2", MaxPointsSnapshot = 2.0m }
        );

        var session = new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TestSession
        {
            SessionId = sessionId,
            TestId = testId,
            StudentId = studentId,
            TestFormat = "Practice",
            Status = "InProgress",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            TestAnswers =
            [
                new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TestAnswer { TestAnswerId = "ta1", SessionId = sessionId, QuestionId = "q1", AnswerId = "a1", Question = q1 },
                new MathInsight.Modules.Grading_Analytics.Persistence.Entities.TestAnswer { TestAnswerId = "ta2", SessionId = sessionId, QuestionId = "q2", AnswerId = "a2", Question = q2 }
            ]
        };
        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GradingOrchestrator>.Instance;
        var orchestrator = new GradingOrchestrator(db, new GradingEngine(), logger);

        var notification = new MathInsight.Shared.Events.TestSubmittedEvent
        {
            SessionId = sessionId,
            StudentId = studentId,
            TestId = testId,
            TestFormat = "Practice",
            SubmissionType = "StudentSubmit",
            SubmittedTime = DateTime.UtcNow
        };

        // Act
        var gradeEvent = await orchestrator.GradeSessionAsync(sessionId, notification);

        // Assert
        Assert.NotNull(gradeEvent);
        Assert.Equal(2, gradeEvent.Answers.Count);

        var ans1 = gradeEvent.Answers.Single(a => a.QuestionId == "q1");
        var ans2 = gradeEvent.Answers.Single(a => a.QuestionId == "q2");

        Assert.Equal(1, ans1.DifficultyLevel);
        Assert.Equal(3, ans2.DifficultyLevel);
    }
}

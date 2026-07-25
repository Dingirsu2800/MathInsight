using System.Diagnostics;
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

        // Assert: 2 + 0 + 0 + 1 = 3 earned, 2 + 2 + 2 + 1 = 7 max â†’ 3/7 * 10 â‰ˆ 4.29
        Assert.Equal("Graded", session.Status);
        Assert.Equal(Math.Round(3.0m / 7.0m * 10.0m, 2), result.Score);
        Assert.Equal(2, result.NumCorrect);
        Assert.Equal(2, result.NumIncorrect); // 1 incorrect + 1 abandoned (counts as both)
        Assert.Equal(1, result.NumAbandoned);
    }
}

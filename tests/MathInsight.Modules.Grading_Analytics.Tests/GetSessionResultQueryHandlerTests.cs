using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Grading_Analytics.Tests;

public sealed class GetSessionResultQueryHandlerTests
{
    private static GradingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D"))
            .Options;
        return new GradingDbContext(options);
    }

    [Fact]
    public async Task Handle_WithAnswerParts_ReturnsSessionResultDtoWithoutNullReferenceException()
    {
        await using var db = CreateDbContext();

        var session = TestDataBuilder.CreateSession(testFormat: "Practice", status: "Graded");
        session.StudentId = "student-123";

        // Add a composite question with answer parts
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 1.0m,
            parts: new List<(string answerKey, string? studentAnswer)>
            {
                ("true", "true"),
                ("false", "true")
            });

        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionResultQueryHandler(db);
        var query = new GetSessionResultQuery(session.SessionId, "student-123");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(session.SessionId, result.SessionId);
        var answer = Assert.Single(result.Answers);
        Assert.Equal(2, answer.AnswerParts.Count);
        Assert.Equal("TRUE_FALSE", answer.AnswerParts[0].PartType);
        Assert.Equal("True", answer.AnswerParts[0].StudentAnswer);
        Assert.Equal("True", answer.AnswerParts[0].CorrectAnswer);
        Assert.Equal("TRUE_FALSE", answer.AnswerParts[1].PartType);
        Assert.Equal("True", answer.AnswerParts[1].StudentAnswer);
        Assert.Equal("False", answer.AnswerParts[1].CorrectAnswer);
    }

    [Fact]
    public async Task Handle_WithSingleChoice_PopulatesAnswerOptions()
    {
        await using var db = CreateDbContext();

        var session = TestDataBuilder.CreateSession(testFormat: "Practice", status: "Graded");
        session.StudentId = "student-123";

        string correctId = "opt-correct";
        TestDataBuilder.AddSingleChoiceAnswer(
            session,
            defaultPoint: 1.0m,
            correctAnswerId: correctId,
            studentAnswerId: correctId);

        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionResultQueryHandler(db);
        var query = new GetSessionResultQuery(session.SessionId, "student-123");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        var answer = Assert.Single(result.Answers);
        Assert.NotEmpty(answer.AnswerOptions);
        var correctOption = Assert.Single(answer.AnswerOptions, o => o.IsCorrect);
        Assert.Equal(correctId, correctOption.AnswerId);
        Assert.True(correctOption.WasSelected);
    }

    [Fact]
    public async Task Handle_InvalidatedQuestion_AwardsTheSnapshotMaximumWithoutChangingMachinePoints()
    {
        await using var db = CreateDbContext();

        var session = TestDataBuilder.CreateSession(testFormat: "Practice", status: "Graded");
        session.StudentId = "student-123";
        var answer = TestDataBuilder.AddShortAnswer(session, 1m, "2", "1");
        answer.PointsEarned = 0m;
        db.TestSessions.Add(session);
        db.TestQuestions.Add(new TestQuestion
        {
            TestId = session.TestId,
            QuestionId = answer.QuestionId,
            MaxPointsSnapshot = 1m,
            IsScoreInvalidated = true
        });
        await db.SaveChangesAsync();

        var result = await new GetSessionResultQueryHandler(db).Handle(
            new GetSessionResultQuery(session.SessionId, "student-123"),
            CancellationToken.None);

        var resultAnswer = Assert.Single(result!.Answers);
        Assert.True(resultAnswer.IsScoreInvalidated);
        Assert.Equal(0m, resultAnswer.MachinePointsEarned);
        Assert.Equal(1m, resultAnswer.EffectivePoints);
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsUnauthorizedAccessException()
    {
        await using var db = CreateDbContext();

        var session = TestDataBuilder.CreateSession(testFormat: "Practice", status: "Graded");
        session.StudentId = "student-123";
        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetSessionResultQueryHandler(db);
        var query = new GetSessionResultQuery(session.SessionId, "other-student");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }
}

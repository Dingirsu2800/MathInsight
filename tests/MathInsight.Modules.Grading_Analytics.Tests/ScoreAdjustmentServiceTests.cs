using System.Text.Json;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Services;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Grading_Analytics.Tests;

public sealed class ScoreAdjustmentServiceTests
{
    [Fact]
    public async Task Adjust_AwardsFullPointsWithoutOverwritingMachineResult()
    {
        await using var db = CreateDbContext();
        var seed = await SeedInvalidReportAsync(db);
        var publisher = new Mock<IPublisher>();
        GradeCalculatedEvent? published = null;
        publisher
            .Setup(item => item.Publish(It.IsAny<GradeCalculatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GradeCalculatedEvent, CancellationToken>((gradeEvent, _) => published = gradeEvent)
            .Returns(Task.CompletedTask);

        var service = new ScoreAdjustmentService(db, publisher.Object);
        await service.AdjustInvalidQuestionVersionAsync(seed.ReportId);

        db.ChangeTracker.Clear();
        var testQuestion = await db.TestQuestions.SingleAsync();
        var answer = await db.TestAnswers.SingleAsync();
        var session = await db.TestSessions.SingleAsync();
        var report = await db.QuestionReports.SingleAsync();

        Assert.True(testQuestion.IsScoreInvalidated);
        Assert.Equal(seed.ReportId, testQuestion.InvalidatedByReportId);
        Assert.Equal(0m, answer.PointsEarned);
        Assert.False(answer.IsCorrect);
        Assert.Equal(10m, session.Score);
        Assert.Equal(0, session.NumCorrect);
        Assert.Equal(0, session.NumIncorrect);
        Assert.Equal(0, session.NumAbandoned);
        Assert.Equal(2, session.GradeRevision);
        Assert.NotNull(report.ScoreAdjustedTime);
        Assert.NotNull(published);
        Assert.Equal(2, published!.GradeRevision);
        var topicEvidence = Assert.Single(published.PerTagResults);
        Assert.Equal(seed.TagId, topicEvidence.TagId);
        Assert.Equal(0m, topicEvidence.TotalItems);
        Assert.Equal(0m, topicEvidence.EarnedPoints);
        Assert.Equal(0m, topicEvidence.MaxPoints);
        var answerEvidence = Assert.Single(published.Answers);
        Assert.True(answerEvidence.IsScoreInvalidated);
        Assert.False(answerEvidence.MachineIsCorrect);
    }

    [Fact]
    public async Task Adjust_WhenPublishingFails_CanRetryWithoutIncrementingRevisionAgain()
    {
        await using var db = CreateDbContext();
        var seed = await SeedInvalidReportAsync(db);
        var publisher = new Mock<IPublisher>();
        publisher
            .SetupSequence(item => item.Publish(It.IsAny<GradeCalculatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient publish failure"))
            .Returns(Task.CompletedTask);

        var service = new ScoreAdjustmentService(db, publisher.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustInvalidQuestionVersionAsync(seed.ReportId));

        db.ChangeTracker.Clear();
        Assert.True((await db.TestQuestions.SingleAsync()).IsScoreInvalidated);
        Assert.Equal(2, (await db.TestSessions.SingleAsync()).GradeRevision);
        Assert.Null((await db.QuestionReports.SingleAsync()).ScoreAdjustedTime);

        await service.AdjustInvalidQuestionVersionAsync(seed.ReportId);

        db.ChangeTracker.Clear();
        Assert.Equal(2, (await db.TestSessions.SingleAsync()).GradeRevision);
        Assert.NotNull((await db.QuestionReports.SingleAsync()).ScoreAdjustedTime);
        publisher.Verify(
            item => item.Publish(It.IsAny<GradeCalculatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Adjust_PublishesRevisionForPrimaryAndSecondaryTags()
    {
        await using var db = CreateDbContext();
        var seed = await SeedInvalidReportAsync(db, includeSecondaryTag: true);
        GradeCalculatedEvent? published = null;
        var publisher = new Mock<IPublisher>();
        publisher
            .Setup(item => item.Publish(It.IsAny<GradeCalculatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GradeCalculatedEvent, CancellationToken>((gradeEvent, _) => published = gradeEvent)
            .Returns(Task.CompletedTask);

        await new ScoreAdjustmentService(db, publisher.Object)
            .AdjustInvalidQuestionVersionAsync(seed.ReportId);

        Assert.NotNull(published);
        var answer = Assert.Single(published!.Answers);
        Assert.Equal(2, answer.TagWeights.Count);
        Assert.Equal(1m, answer.TagWeights.Sum(item => item.Weight));
        Assert.Equal(2, published.PerTagResults.Count);
        Assert.All(published.PerTagResults, result =>
        {
            Assert.Equal(0m, result.TotalItems);
            Assert.Equal(0m, result.MaxPoints);
        });
    }

    private static GradingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase($"score-adjustment-{Guid.NewGuid():D}")
            .Options;
        return new GradingDbContext(options);
    }

    private static async Task<SeedData> SeedInvalidReportAsync(
        GradingDbContext db,
        bool includeSecondaryTag = false)
    {
        const string reportId = "report_01";
        const string questionId = "question_01";
        const string versionId = "version_01";
        const string testId = "test_01";
        const string sessionId = "session_01";
        const string tagId = "TOPIC-G12-DERIVAPP";
        const decimal maxPoints = 2.5m;

        var topics = new List<QuestionTopicSnapshot> { new(tagId, true) };
        if (includeSecondaryTag)
            topics.Add(new QuestionTopicSnapshot("TOPIC-G12-SECONDARY", false));

        var snapshot = new QuestionSnapshotV2(
            questionId,
            "SINGLE_CHOICE",
            "DIFF-MEDIUM",
            12,
            1m,
            topics,
            [
                new QuestionAnswerSnapshot("answer_correct", "Correct", true),
                new QuestionAnswerSnapshot("answer_wrong", "Wrong", false)
            ],
            []);

        var version = new QuestionVersion
        {
            VersionId = versionId,
            QuestionId = questionId,
            QuestionContent = "Immutable question",
            QuestionAnswer = "answer_correct",
            AnswersSnapshot = JsonSerializer.Serialize(snapshot),
            VersionNumber = 1,
            SnapshotSchemaVersion = 2
        };
        var question = new Question
        {
            QuestionId = questionId,
            QuestionType = "SINGLE_CHOICE",
            DifficultyId = "DIFF-MEDIUM",
            DefaultWeight = 1m,
            QuestionContent = "Current question",
            QuestionTopics =
            [
                new QuestionTopic
                {
                    QuestionTopicId = "question_topic_01",
                    QuestionId = questionId,
                    TagId = tagId,
                    IsPrimary = true
                }
            ]
        };
        if (includeSecondaryTag)
        {
            question.QuestionTopics.Add(new QuestionTopic
            {
                QuestionTopicId = "question_topic_02",
                QuestionId = questionId,
                TagId = "TOPIC-G12-SECONDARY",
                IsPrimary = false
            });
        }
        var session = new TestSession
        {
            SessionId = sessionId,
            TestId = testId,
            StudentId = "student_01",
            TestFormat = "Exam",
            Status = "Graded",
            SubmissionType = "StudentSubmit",
            TotalQuestion = 1,
            NumIncorrect = 1,
            Score = 0m,
            GradeRevision = 1
        };
        var machineAnswer = new TestAnswer
        {
            TestAnswerId = "test_answer_01",
            SessionId = sessionId,
            QuestionId = questionId,
            QuestionNo = 1,
            AnswerId = "answer_wrong",
            IsCorrect = false,
            PointsEarned = 0m,
            Question = question,
            Session = session
        };
        session.TestAnswers.Add(machineAnswer);

        db.AddRange(
            version,
            question,
            new TestQuestion
            {
                TestId = testId,
                QuestionId = questionId,
                QuestionVersionId = versionId,
                WeightSnapshot = 1m,
                MaxPointsSnapshot = maxPoints,
                ScoringRuleSnapshot = "AllOrNothing",
                QuestionVersion = version
            },
            session,
            new QuestionReport
            {
                ReportId = reportId,
                QuestionId = questionId,
                ReporterRole = "Student",
                ReportReason = "Wrong answer key",
                Status = "Resolved",
                SessionId = sessionId,
                QuestionVersionId = versionId,
                ResolutionAction = "InvalidateAndAwardFull"
            });
        await db.SaveChangesAsync();
        return new SeedData(reportId, tagId, maxPoints);
    }

    private sealed record SeedData(string ReportId, string TagId, decimal MaxPoints);
}

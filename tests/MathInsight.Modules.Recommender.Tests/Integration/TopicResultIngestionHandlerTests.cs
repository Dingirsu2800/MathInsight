using MathInsight.Modules.Recommender.Handlers;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

public sealed class TopicResultIngestionHandlerTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly TopicResultIngestionHandler _handler;

    public TopicResultIngestionHandlerTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D"))
            .Options;
        _db = new RecommenderDbContext(options);
        _handler = new TopicResultIngestionHandler(_db, new CompetencyEngine(_db));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SemanticIds_CreateWeightedSnapshot_AndDuplicateIsIdempotent()
    {
        await SeedStudentAndTagAsync("student_01", 12, "TOPIC-G12-DERIVAPP", 12);
        var evt = MakeExamEvent(
            "student_01",
            "session_01",
            "TOPIC-G12-DERIVAPP",
            topicScore: 7.50m,
            totalItems: 2m,
            correctItems: 1m,
            earnedPoints: 1.50m,
            maxPoints: 2.00m);

        await _handler.Handle(evt, default);
        await _handler.Handle(evt, default);

        var snapshot = Assert.Single(await _db.StudentTopicSessionResults.ToListAsync());
        Assert.Equal("student_01", snapshot.StudentId);
        Assert.Equal("session_01", snapshot.SessionId);
        Assert.Equal("TOPIC-G12-DERIVAPP", snapshot.TagId);
        Assert.Equal(2m, snapshot.TotalItems);
        Assert.Equal(1m, snapshot.CorrectItems);
        Assert.Equal(1.50m, snapshot.EarnedPoints);
        Assert.Equal(2.00m, snapshot.MaxPoints);
        Assert.Equal(7.50m, snapshot.TopicScore);

        var mastery = Assert.Single(await _db.TagsMasteries.ToListAsync());
        Assert.Equal(2, mastery.NumberDone);
        Assert.Equal(1, mastery.NumCorrect);
    }

    [Fact]
    public async Task Exam_UpdatesAnchorOfficialPointAndCompetencyForCurrentGrade()
    {
        await SeedStudentAndTagAsync("student_01", 12, "TOPIC-G12", 12);
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = "TOPIC-G11",
            TagName = "Grade 11",
            Grade = 11,
            IsActive = true
        });
        _db.TagsMasteries.Add(Mastery("student_01", "TOPIC-G11", 10m));
        await _db.SaveChangesAsync();

        await _handler.Handle(
            MakeExamEvent("student_01", "session_01", "TOPIC-G12", topicScore: 9m),
            default);

        var mastery = await _db.TagsMasteries.SingleAsync(item => item.TagId == "TOPIC-G12");
        Assert.Equal(9m, mastery.ExamAnchor);
        Assert.Equal(7.80m, mastery.OfficialPoint);
        Assert.Equal("Mastered", mastery.MasteryStatus);
        Assert.Null(mastery.LastPracticedTime);
        Assert.NotNull(mastery.LastCalculatedAt);

        var competency = Assert.Single(await _db.CompetencyPoints.ToListAsync());
        Assert.Equal(12, competency.Grade);
        Assert.Equal(7.80m, competency.Point);
    }

    [Fact]
    public async Task Practice_UpdatesAccuracyAsPercentAndLastPracticedTime()
    {
        await SeedStudentAndTagAsync("student_01", 12, "TOPIC-G12", 12);
        var gradedAt = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc);
        var evt = MakePracticeEvent("student_01", "session_01", "TOPIC-G12", gradedAt);

        await _handler.Handle(evt, default);

        var mastery = Assert.Single(await _db.TagsMasteries.ToListAsync());
        Assert.Equal(2, mastery.NumberDone);
        Assert.Equal(1, mastery.NumCorrect);
        Assert.Equal(50.00m, mastery.AccuracyRate);
        Assert.Equal(gradedAt, mastery.LastPracticedTime);
        Assert.Equal(gradedAt, mastery.LastCalculatedAt);
        Assert.InRange(mastery.OfficialPoint, 0m, 10m);
    }

    [Fact]
    public async Task StudentWithoutCurrentGrade_PersistsMasteryWithoutCompetency()
    {
        await SeedStudentAndTagAsync("student_01", null, "TOPIC-G12", 12);

        await _handler.Handle(
            MakeExamEvent("student_01", "session_01", "TOPIC-G12", topicScore: 3m),
            default);

        Assert.Single(await _db.TagsMasteries.ToListAsync());
        Assert.Single(await _db.StudentTopicSessionResults.ToListAsync());
        Assert.Empty(await _db.CompetencyPoints.ToListAsync());
    }

    private async Task SeedStudentAndTagAsync(
        string studentId,
        int? currentGrade,
        string tagId,
        int tagGrade)
    {
        _db.Students.Add(new StudentReadOnly
        {
            StudentId = studentId,
            CurrentGrade = currentGrade
        });
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
            TagName = tagId,
            Grade = tagGrade,
            IsActive = true
        });
        await _db.SaveChangesAsync();
    }

    private static GradeCalculatedEvent MakeExamEvent(
        string studentId,
        string sessionId,
        string tagId,
        decimal topicScore,
        decimal totalItems = 1m,
        decimal correctItems = 1m,
        decimal earnedPoints = 1m,
        decimal maxPoints = 1m)
        => new()
        {
            StudentId = studentId,
            SessionId = sessionId,
            TestId = "test_01",
            TestFormat = "Exam",
            GradedAt = DateTime.UtcNow,
            PerTagResults =
            [
                new TopicGradeResult
                {
                    TagId = tagId,
                    TopicScore = topicScore,
                    TotalItems = totalItems,
                    CorrectItems = correctItems,
                    EarnedPoints = earnedPoints,
                    MaxPoints = maxPoints
                }
            ]
        };

    private static GradeCalculatedEvent MakePracticeEvent(
        string studentId,
        string sessionId,
        string tagId,
        DateTime gradedAt)
        => new()
        {
            StudentId = studentId,
            SessionId = sessionId,
            TestId = "test_01",
            TestFormat = "Practice",
            GradedAt = gradedAt,
            PerTagResults =
            [
                new TopicGradeResult
                {
                    TagId = tagId,
                    TopicScore = 5m,
                    TotalItems = 2m,
                    CorrectItems = 1m,
                    EarnedPoints = 1m,
                    MaxPoints = 2m
                }
            ],
            Answers =
            [
                new GradedAnswerDto
                {
                    QuestionId = "question_01",
                    TagId = tagId,
                    IsCorrect = true,
                    PointsEarned = 1m,
                    MaxPoints = 1m,
                    DifficultyLevel = 2,
                    TimeSpent = 10,
                    QuestionNo = 1
                },
                new GradedAnswerDto
                {
                    QuestionId = "question_02",
                    TagId = tagId,
                    IsCorrect = false,
                    PointsEarned = 0m,
                    MaxPoints = 1m,
                    DifficultyLevel = 1,
                    TimeSpent = 10,
                    QuestionNo = 2
                }
            ]
        };

    private static TagsMastery Mastery(string studentId, string tagId, decimal officialPoint)
        => new()
        {
            TagsMasteryId = Guid.NewGuid().ToString("D"),
            StudentId = studentId,
            TagId = tagId,
            OfficialPoint = officialPoint,
            PracticePoint = officialPoint,
            ExamAnchor = officialPoint,
            MasteryStatus = "Mastered",
            RecommendedDifficultyLevel = 4
        };
}

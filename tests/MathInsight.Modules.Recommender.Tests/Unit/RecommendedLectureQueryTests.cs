using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

public sealed class RecommendedLectureQueryTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly GetRecommendedLecturesQueryHandler _handler;

    public RecommendedLectureQueryTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase($"recommended-lectures-{Guid.NewGuid()}")
            .Options;

        _db = new RecommenderDbContext(options);
        _handler = new GetRecommendedLecturesQueryHandler(_db);
    }

    [Fact]
    public async Task Handle_PersonalizedTopic_PrefersExactThenNearestLower_AndNeverHarder()
    {
        const string studentId = "student_01";
        AddActiveTopic("topic-derivative", grade: 12);
        AddDifficulty("diff-2", level: 2);
        AddDifficulty("diff-3", level: 3);
        AddDifficulty("diff-4", level: 4);
        AddMastery(studentId, "topic-derivative", officialPoint: 4.20m, numberDone: 3, targetLevel: 3);
        AddLecture("lecture-l2", "topic-derivative", "diff-2", likes: 99);
        AddLecture("lecture-l3", "topic-derivative", "diff-3", likes: 1);
        AddLecture("lecture-l4", "topic-derivative", "diff-4", likes: 200);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        Assert.Equal(new[] { "lecture-l3", "lecture-l2" }, result.Select(x => x.LectureId));
        Assert.Equal(new[] { "WeakTopicExactDifficulty", "WeakTopicLowerDifficultyFallback" }, result.Select(x => x.Reason));
        Assert.All(result, x => Assert.True(x.DifficultyLevel <= x.TargetDifficultyLevel));
        Assert.True(result[1].IsDifficultyFallback);
        Assert.Equal(4.20m, result[0].OfficialPoint);
        Assert.Equal(3, result[0].EvidenceCount);
    }

    [Fact]
    public async Task Handle_FiltersInactiveTaxonomyAndUnpublishedLectures()
    {
        const string studentId = "student_01";
        AddActiveTopic("topic-active", grade: 12);
        AddActiveTopic("topic-inactive", grade: 12, isActive: false);
        AddDifficulty("diff-active", level: 2);
        AddDifficulty("diff-inactive", level: 2, isActive: false);
        AddMastery(studentId, "topic-active", officialPoint: 6m, numberDone: 3, targetLevel: 2);
        AddMastery(studentId, "topic-inactive", officialPoint: 4m, numberDone: 3, targetLevel: 2);
        AddLecture("lecture-active", "topic-active", "diff-active");
        AddLecture("lecture-draft", "topic-active", "diff-active", status: "Draft");
        AddLecture("lecture-inactive-difficulty", "topic-active", "diff-inactive");
        AddLecture("lecture-inactive-topic", "topic-inactive", "diff-active");
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        var lecture = Assert.Single(result);
        Assert.Equal("lecture-active", lecture.LectureId);
        Assert.Equal("ProgressionExactDifficulty", lecture.Reason);
    }

    [Fact]
    public async Task Handle_InsufficientEvidence_UsesGradeFoundationColdStart()
    {
        const string studentId = "student_01";
        AddStudent(studentId, currentGrade: 12);
        AddActiveTopic("topic-grade-12", grade: 12);
        AddDifficulty("diff-1", level: 1);
        AddMastery(studentId, "topic-grade-12", officialPoint: 2m, numberDone: 2, targetLevel: 1);
        AddLecture("lecture-foundation", "topic-grade-12", "diff-1", likes: 4);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        var lecture = Assert.Single(result);
        Assert.Equal("ColdStartGradeFoundation", lecture.Reason);
        Assert.Null(lecture.OfficialPoint);
        Assert.Equal(0, lecture.EvidenceCount);
        Assert.Equal((byte)1, lecture.TargetDifficultyLevel);
    }

    [Fact]
    public async Task Handle_PersonalizedResults_ArePrioritizedDiverseAndGloballyLimited()
    {
        const string studentId = "student_01";
        AddDifficulty("diff-2", level: 2);
        AddDifficulty("diff-3", level: 3);

        for (var topicNo = 1; topicNo <= 4; topicNo++)
        {
            var topicId = $"topic-{topicNo}";
            AddActiveTopic(topicId, grade: 12);
            AddMastery(studentId, topicId, topicNo == 1 ? 4m : topicNo == 2 ? 6m : 8m, 3, 2);
            AddLecture($"lecture-{topicNo}-a", topicId, "diff-2", likes: 10);
            AddLecture($"lecture-{topicNo}-b", topicId, "diff-2", likes: 5);
            AddLecture($"lecture-{topicNo}-lower", topicId, "diff-3", likes: 99);
        }

        await _db.SaveChangesAsync();
        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        Assert.Equal(6, result.Count);
        Assert.All(result.GroupBy(x => x.TagId), group => Assert.InRange(group.Count(), 1, 2));
        Assert.Equal("topic-1", result[0].TagId);
        Assert.All(result.Take(2), x => Assert.StartsWith("WeakTopic", x.Reason));
        Assert.Equal("lecture-1-a", result[0].LectureId);
        Assert.Equal(
            new[] { "topic-1", "topic-1", "topic-2", "topic-2", "topic-3", "topic-3" },
            result.Select(x => x.TagId));
        Assert.DoesNotContain(result, x => x.DifficultyLevel > x.TargetDifficultyLevel);
    }

    [Fact]
    public async Task Handle_TiedCandidates_UsesLectureIdAsFinalTieBreaker()
    {
        const string studentId = "student_01";
        var updatedTime = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);
        AddActiveTopic("topic-tie", grade: 12);
        AddDifficulty("diff-2", level: 2);
        AddMastery(studentId, "topic-tie", officialPoint: 6m, numberDone: 3, targetLevel: 2);
        AddLecture("lecture-b", "topic-tie", "diff-2", likes: 5, updatedTime: updatedTime);
        AddLecture("lecture-a", "topic-tie", "diff-2", likes: 5, updatedTime: updatedTime);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        Assert.Equal(new[] { "lecture-a", "lecture-b" }, result.Select(x => x.LectureId));
    }

    [Fact]
    public async Task Handle_ColdStart_UsesStudentGradeOnly_AndHandlesMissingGrade()
    {
        const string studentId = "student_01";
        AddStudent(studentId, currentGrade: 12);
        AddActiveTopic("topic-grade-12", grade: 12);
        AddActiveTopic("topic-grade-11", grade: 11);
        AddDifficulty("diff-1", level: 1);
        AddLecture("lecture-grade-12-low", "topic-grade-12", "diff-1", likes: 1);
        AddLecture("lecture-grade-12-high", "topic-grade-12", "diff-1", likes: 9);
        AddLecture("lecture-grade-11", "topic-grade-11", "diff-1", likes: 100);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);

        Assert.Equal(new[] { "lecture-grade-12-high", "lecture-grade-12-low" }, result.Select(x => x.LectureId));

        _db.Students.Single().CurrentGrade = null;
        await _db.SaveChangesAsync();

        var noGrade = await _handler.Handle(new GetRecommendedLecturesQuery(studentId), CancellationToken.None);
        Assert.Empty(noGrade);
    }

    private void AddStudent(string studentId, int? currentGrade)
    {
        _db.Students.Add(new StudentReadOnly { StudentId = studentId, CurrentGrade = currentGrade });
    }

    private void AddActiveTopic(string tagId, int grade, bool isActive = true)
    {
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
            TagName = tagId,
            Grade = grade,
            IsActive = isActive
        });
    }

    private void AddDifficulty(string difficultyId, int level, bool isActive = true)
    {
        _db.TagDifficulties.Add(new TagDifficultyReadOnly
        {
            DifficultyId = difficultyId,
            DifficultyName = difficultyId,
            LevelValue = level,
            IsActive = isActive
        });
    }

    private void AddMastery(string studentId, string tagId, decimal officialPoint, int numberDone, byte targetLevel)
    {
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = $"mastery-{studentId}-{tagId}",
            StudentId = studentId,
            TagId = tagId,
            OfficialPoint = officialPoint,
            NumberDone = numberDone,
            RecommendedDifficultyLevel = targetLevel
        });
    }

    private void AddLecture(
        string lectureId,
        string tagId,
        string difficultyId,
        int likes = 0,
        string status = "Published",
        DateTime? updatedTime = null)
    {
        _db.Lectures.Add(new LectureReadOnly
        {
            LectureId = lectureId,
            Title = lectureId,
            ThumbnailUrl = null,
            TagId = tagId,
            DifficultyId = difficultyId,
            Likes = likes,
            Status = status,
            UpdatedTime = updatedTime ?? DateTime.UtcNow
        });
    }

    public void Dispose() => _db.Dispose();
}

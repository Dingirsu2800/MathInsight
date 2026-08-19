using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

public sealed class StudentRecommendationProviderTests : IDisposable
{
    private const string RootTagId = "root-topic";
    private readonly RecommenderDbContext _db;
    private readonly RecommenderService _sut;

    public StudentRecommendationProviderTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        _db = new RecommenderDbContext(options);
        _sut = new RecommenderService(_db, new DifficultyMappingService());
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = RootTagId,
            TagName = "Root topic",
            Grade = 12,
            IsActive = true
        });
    }

    [Fact]
    public async Task GetWeakTagAdviceAsync_ReturnsOnlyActiveQualifiedRows()
    {
        Seed("TOPIC-QUALIFIED", 4.20m, numberDone: 3, isActive: true, level: 2);
        Seed("TOPIC-TOO-LITTLE-EVIDENCE", 2.00m, numberDone: 2, isActive: true, level: 1);
        Seed("TOPIC-NOT-WEAK", 5.00m, numberDone: 10, isActive: true, level: 2);
        Seed("TOPIC-INACTIVE", 1.00m, numberDone: 10, isActive: false, level: 1);
        await _db.SaveChangesAsync();

        var result = await _sut.GetWeakTagAdviceAsync("student_01");

        var advice = Assert.Single(result);
        Assert.Equal("TOPIC-QUALIFIED", advice.TagId);
        Assert.Equal(4.20m, advice.OfficialPoint);
        Assert.Equal(3, advice.EvidenceCount);
        Assert.Equal((byte)2, advice.RecommendedDifficultyLevel);
        Assert.Equal("OfficialPointBelow5", advice.Reason);
        Assert.IsAssignableFrom<IStudentRecommendationProvider>(_sut);
    }

    [Fact]
    public async Task GetWeakTagAdviceAsync_OrdersByPointThenOrdinalTagId()
    {
        Seed("TOPIC-B", 2.50m, numberDone: 3, isActive: true, level: 1);
        Seed("TOPIC-A", 2.50m, numberDone: 3, isActive: true, level: 1);
        Seed("TOPIC-C", 1.00m, numberDone: 3, isActive: true, level: 1);
        await _db.SaveChangesAsync();

        var result = await _sut.GetWeakTagAdviceAsync("student_01");

        Assert.Equal(["TOPIC-C", "TOPIC-A", "TOPIC-B"], result.Select(item => item.TagId));
    }

    [Fact]
    public async Task GetWeakTagAdviceAsync_RootTopicIsNotReturnedAsPracticeAdvice()
    {
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = "mastery-root-topic",
            StudentId = "student_01",
            TagId = RootTagId,
            OfficialPoint = 1.00m,
            PracticePoint = 1.00m,
            ExamAnchor = 1.00m,
            MasteryStatus = "Learning",
            NumberDone = 3,
            RecommendedDifficultyLevel = 1
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetWeakTagAdviceAsync("student_01");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopicMasteryAdviceAsync_ReturnsOnlyRequestedActiveDirectChildren()
    {
        Seed("TOPIC-REQUESTED", 7.60m, numberDone: 8, isActive: true, level: 4);
        Seed("TOPIC-NOT-REQUESTED", 2.00m, numberDone: 8, isActive: true, level: 1);
        Seed("TOPIC-INACTIVE", 3.00m, numberDone: 8, isActive: false, level: 2);
        SeedSession("session-valid-1", "TOPIC-REQUESTED", totalItems: 10m);
        SeedSession("session-valid-2", "TOPIC-REQUESTED", totalItems: 5m);
        SeedSession("session-not-requested", "TOPIC-NOT-REQUESTED", totalItems: 20m);
        SeedSession("session-empty", "TOPIC-REQUESTED", totalItems: 0m);
        await _db.SaveChangesAsync();

        var result = await _sut.GetTopicMasteryAdviceAsync(
            "student_01",
            ["TOPIC-REQUESTED", "missing-topic"],
            CancellationToken.None);

        var advice = Assert.Single(result);
        Assert.Equal("TOPIC-REQUESTED", advice.Key);
        Assert.Equal(7.60m, advice.Value.OfficialPoint);
        Assert.Equal(8, advice.Value.EvidenceItemCount);
        Assert.Equal(2, advice.Value.EvidenceSessionCount);
        Assert.Equal((byte)4, advice.Value.RecommendedDifficultyLevel);
        Assert.IsAssignableFrom<IStudentTopicMasteryProvider>(_sut);
    }

    public void Dispose() => _db.Dispose();

    private void Seed(string tagId, decimal point, int numberDone, bool isActive, byte level)
    {
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
            ParentTagId = RootTagId,
            TagName = tagId,
            Grade = 12,
            IsActive = isActive
        });
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = $"mastery-{tagId}",
            StudentId = "student_01",
            TagId = tagId,
            OfficialPoint = point,
            PracticePoint = point,
            ExamAnchor = 5.00m,
            MasteryStatus = "Learning",
            NumberDone = numberDone,
            RecommendedDifficultyLevel = level
        });
    }

    private void SeedSession(string sessionId, string tagId, decimal totalItems)
    {
        _db.StudentTopicSessionResults.Add(new StudentTopicSessionResult
        {
            StudentTopicSessionResultId = $"snapshot-{sessionId}",
            StudentId = "student_01",
            SessionId = sessionId,
            TagId = tagId,
            TotalItems = totalItems,
            CorrectItems = totalItems > 0 ? 5m : 0m,
            EarnedPoints = totalItems > 0 ? 5m : 0m,
            MaxPoints = totalItems > 0 ? 10m : 0m,
            TopicScore = totalItems > 0 ? 5m : 0m,
            CreatedTime = DateTime.UtcNow
        });
    }
}

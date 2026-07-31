using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

public sealed class StudentRecommendationProviderTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly RecommenderService _sut;

    public StudentRecommendationProviderTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        _db = new RecommenderDbContext(options);
        _sut = new RecommenderService(_db, new DifficultyMappingService());
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

    public void Dispose() => _db.Dispose();

    private void Seed(string tagId, decimal point, int numberDone, bool isActive, byte level)
    {
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
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
}

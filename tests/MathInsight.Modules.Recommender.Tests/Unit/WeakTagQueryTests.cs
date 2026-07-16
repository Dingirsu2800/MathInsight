using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

public sealed class WeakTagQueryTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly RecommenderService _service;

    public WeakTagQueryTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D"))
            .Options;
        _db = new RecommenderDbContext(options);
        _service = new RecommenderService(_db, new DifficultyMappingService());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetStudentWeakTagsAsync_ReturnsOnlyActiveTopicsBelowThreshold()
    {
        const string studentId = "student_01";
        AddTag("TOPIC-WEAK", "Algebra", isActive: true);
        AddTag("TOPIC-STRONG", "Geometry", isActive: true);
        AddTag("TOPIC-INACTIVE", "Legacy", isActive: false);
        AddMastery(studentId, "TOPIC-WEAK", 4.99m);
        AddMastery(studentId, "TOPIC-STRONG", 5.00m);
        AddMastery(studentId, "TOPIC-INACTIVE", 1.00m);
        await _db.SaveChangesAsync();

        var result = await _service.GetStudentWeakTagsAsync(studentId);

        var weakTag = Assert.Single(result);
        Assert.Equal("TOPIC-WEAK", weakTag.TagId);
    }

    [Fact]
    public async Task GetStudentWeakTagsAsync_NoMasteryRows_ReturnsEmpty()
    {
        AddTag("TOPIC-NEW", "Statistics", isActive: true);
        await _db.SaveChangesAsync();

        var result = await _service.GetStudentWeakTagsAsync("student_01");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStudentWeakTagsAsync_OrdersLowestPointFirst()
    {
        const string studentId = "student_01";
        AddTag("TOPIC-1", "T1", true);
        AddTag("TOPIC-2", "T2", true);
        AddTag("TOPIC-3", "T3", true);
        AddMastery(studentId, "TOPIC-1", 3.00m);
        AddMastery(studentId, "TOPIC-2", 1.00m);
        AddMastery(studentId, "TOPIC-3", 4.50m);
        await _db.SaveChangesAsync();

        var result = await _service.GetStudentWeakTagsAsync(studentId);

        Assert.Equal([1.00m, 3.00m, 4.50m], result.Select(item => item.OfficialPoint));
    }

    [Fact]
    public async Task GetWeakTagAdviceAsync_ReturnsSharedAdaptiveContract()
    {
        AddTag("TOPIC-G12-DERIVAPP", "Derivative applications", isActive: true);
        AddMastery(
            "student_01",
            "TOPIC-G12-DERIVAPP",
            2.50m,
            recommendedDifficultyLevel: 1);
        await _db.SaveChangesAsync();

        var result = await _service.GetWeakTagAdviceAsync("student_01");

        var advice = Assert.Single(result);
        Assert.Equal("TOPIC-G12-DERIVAPP", advice.TagId);
        Assert.True(advice.IsWeak);
        Assert.True(advice.IsRemedial);
        Assert.Equal((byte)1, advice.RecommendedDifficultyLevel);
        Assert.Equal("RemedialLevel1", advice.Reason);
    }

    private void AddTag(string tagId, string name, bool isActive)
        => _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
            TagName = name,
            Grade = 12,
            IsActive = isActive
        });

    private void AddMastery(
        string studentId,
        string tagId,
        decimal officialPoint,
        byte recommendedDifficultyLevel = 2)
        => _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = Guid.NewGuid().ToString("D"),
            StudentId = studentId,
            TagId = tagId,
            OfficialPoint = officialPoint,
            PracticePoint = officialPoint,
            ExamAnchor = officialPoint,
            MasteryStatus = officialPoint < 5m ? "Learning" : "Mastered",
            RecommendedDifficultyLevel = recommendedDifficultyLevel
        });
}

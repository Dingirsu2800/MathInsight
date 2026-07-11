using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Unit tests for RecommenderService.GetStudentWeakTagsAsync (UC-52).
/// Uses InMemory EF provider to avoid SQL Server dependency.
///
/// Rule: WeakTag ↔ official_point &lt; 5.00.
/// No-row behavior: topics without a TagsMastery row are NOT returned as weak.
/// </summary>
public class WeakTagQueryTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly RecommenderService _sut;

    public WeakTagQueryTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new RecommenderDbContext(options);
        _sut = new RecommenderService(_db, new DifficultyMappingService());
    }

    public void Dispose() => _db.Dispose();

    private static TagTopicReadOnly MakeTagTopic(Guid tagId, string name) =>
        new() { TagId = tagId, TagName = name };

    private static TagsMastery MakeMastery(Guid studentId, Guid tagId, decimal officialPoint) =>
        new()
        {
            TagsMasteryId  = Guid.NewGuid(),
            StudentId      = studentId,
            TagId          = tagId,
            OfficialPoint  = officialPoint,
            PracticePoint  = officialPoint,
            ExamAnchor     = officialPoint,
            MasteryStatus  = officialPoint < 5m ? "Learning" : "Mastered",
        };

    [Fact]
    public async Task GetStudentWeakTagsAsync_ReturnsOnlyBelowThreshold()
    {
        var studentId = Guid.NewGuid();
        var weakTagId = Guid.NewGuid();
        var strongTagId = Guid.NewGuid();

        _db.TagTopics.AddRange(
            MakeTagTopic(weakTagId,   "Algebra"),
            MakeTagTopic(strongTagId, "Geometry"));
        _db.TagsMasteries.AddRange(
            MakeMastery(studentId, weakTagId,   4.99m),  // weak
            MakeMastery(studentId, strongTagId, 5.00m)); // not weak
        await _db.SaveChangesAsync();

        var result = await _sut.GetStudentWeakTagsAsync(studentId);

        Assert.Single(result);
        Assert.Equal(weakTagId, result[0].TagId);
    }

    [Fact]
    public async Task GetStudentWeakTagsAsync_ExactlyAt5_IsNotWeak()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _db.TagTopics.Add(MakeTagTopic(tagId, "Calculus"));
        _db.TagsMasteries.Add(MakeMastery(studentId, tagId, 5.00m));
        await _db.SaveChangesAsync();

        var result = await _sut.GetStudentWeakTagsAsync(studentId);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStudentWeakTagsAsync_NoRows_ReturnsEmpty()
    {
        // No TagsMastery row → not returned as weak (no-row behavior)
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _db.TagTopics.Add(MakeTagTopic(tagId, "Statistics"));
        // Intentionally no TagsMastery row added
        await _db.SaveChangesAsync();

        var result = await _sut.GetStudentWeakTagsAsync(studentId);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStudentWeakTagsAsync_OrderedByOfficialPointAscending()
    {
        var studentId = Guid.NewGuid();
        var tag1 = Guid.NewGuid();
        var tag2 = Guid.NewGuid();
        var tag3 = Guid.NewGuid();

        _db.TagTopics.AddRange(
            MakeTagTopic(tag1, "T1"),
            MakeTagTopic(tag2, "T2"),
            MakeTagTopic(tag3, "T3"));
        _db.TagsMasteries.AddRange(
            MakeMastery(studentId, tag1, 3.00m),
            MakeMastery(studentId, tag2, 1.00m),
            MakeMastery(studentId, tag3, 4.50m));
        await _db.SaveChangesAsync();

        var result = await _sut.GetStudentWeakTagsAsync(studentId);

        Assert.Equal(3, result.Count);
        Assert.Equal(1.00m, result[0].OfficialPoint);
        Assert.Equal(3.00m, result[1].OfficialPoint);
        Assert.Equal(4.50m, result[2].OfficialPoint);
    }
}

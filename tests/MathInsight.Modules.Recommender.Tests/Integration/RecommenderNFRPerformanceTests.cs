using System.Diagnostics;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;
using MathInsight.Modules.Recommender.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

/// <summary>
/// TC-SYS-NFR-RCM-002..003
/// Performance (NFR) tests for Recommender module.
/// Validates SLA thresholds using Stopwatch with InMemory EF and direct handler queries.
/// </summary>
public sealed class RecommenderNFRPerformanceTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly string _studentId;

    public RecommenderNFRPerformanceTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RecommenderDbContext(options);
        _studentId = Guid.NewGuid().ToString();

        SeedWeakTagsWithLecturesAsync().GetAwaiter().GetResult();
    }

    private async Task SeedWeakTagsWithLecturesAsync()
    {
        const int weakTagCount = 30;

        for (int i = 0; i < weakTagCount; i++)
        {
            var tagId    = Guid.NewGuid().ToString();
            var lectureId = Guid.NewGuid().ToString();
            var matId    = Guid.NewGuid().ToString();

            _db.TagTopics.Add(new TagTopicReadOnly
            {
                TagId   = tagId,
                TagName = $"Topic_{i:D3}",
                Grade   = 10
            });

            _db.TagsMasteries.Add(new TagsMastery
            {
                TagsMasteryId              = Guid.NewGuid().ToString(),
                StudentId                  = _studentId,
                TagId                      = tagId,
                OfficialPoint              = Math.Round(0.50m + (i * 0.13m), 2), // all < 5.00
                PracticePoint              = 3.00m,
                ExamAnchor                 = 2.00m,
                MasteryStatus              = "Learning",
                NumberDone                 = 10,
                SeriesAnswerCount          = 0,
                RecommendedDifficultyLevel = i < 15 ? (byte)1 : (byte)2,
                ExamHistory                = "[]"
            });

            // LectureReadOnly: Status must be 'Published' (handler filters l.Status == "Published")
            _db.Lectures.Add(new LectureReadOnly
            {
                LectureId = lectureId,
                Title     = $"Lecture for Topic_{i:D3}",
                TagId     = tagId,
                Status    = "Published"
            });

            // MaterialReadOnly: MaterialName (not MaterialTitle)
            _db.Materials.Add(new MaterialReadOnly
            {
                MaterialId   = matId,
                MaterialName = $"Material for Topic_{i:D3}",
                Status       = "Active"
            });

            // LectureMaterialReadOnly: LectureId, MaterialId only (no LectureMaterialId)
            _db.LectureMaterials.Add(new LectureMaterialReadOnly
            {
                LectureId  = lectureId,
                MaterialId = matId
            });
        }

        await _db.SaveChangesAsync();
    }

    public void Dispose() => _db.Dispose();

    // ── TC-SYS-NFR-RCM-002 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-NFR-RCM-002: NFR-P-RCM-02 — GetRecommendedLectures with 30 weak tags &lt; 500ms.
    /// Uses GetRecommendedLecturesQueryHandler directly (RecommenderService has no such method).
    /// </summary>
    [Fact]
    public async Task GetRecommendedLectures_30WeakTags_CompletesWithin500Ms()
    {
        var handler = new GetRecommendedLecturesQueryHandler(_db, new DifficultyMappingService());

        var sw = Stopwatch.StartNew();
        var lectures = await handler.Handle(new GetRecommendedLecturesQuery(_studentId), default);
        sw.Stop();

        Assert.True(sw.Elapsed.TotalMilliseconds < 500,
            $"GetRecommendedLectures took {sw.Elapsed.TotalMilliseconds:F1}ms, exceeds 500ms SLA");

        Assert.Equal(30, lectures.Count);
    }

    // ── TC-SYS-NFR-RCM-003 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-NFR-RCM-003: NFR-P-RCM-03 — GetRecommendedMaterials with 30 weak tags &lt; 500ms.
    /// Uses GetRecommendedMaterialsQueryHandler directly.
    /// </summary>
    [Fact]
    public async Task GetRecommendedMaterials_30WeakTags_CompletesWithin500Ms()
    {
        var handler = new GetRecommendedMaterialsQueryHandler(_db, new DifficultyMappingService());

        var sw = Stopwatch.StartNew();
        var materials = await handler.Handle(new GetRecommendedMaterialsQuery(_studentId), default);
        sw.Stop();

        Assert.True(sw.Elapsed.TotalMilliseconds < 500,
            $"GetRecommendedMaterials took {sw.Elapsed.TotalMilliseconds:F1}ms, exceeds 500ms SLA");

        Assert.Equal(30, materials.Count);
    }
}

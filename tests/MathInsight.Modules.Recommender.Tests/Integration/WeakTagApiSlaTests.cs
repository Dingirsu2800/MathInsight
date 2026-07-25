using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

/// <summary>
/// Performance SLA test for WeakTag API (G4 — SC SLA).
/// Verifies that GET /weak-tags returns within 2 seconds for a student
/// with 50+ TagsMastery rows, using SQL (InMemory) only — no Redis.
/// </summary>
public class WeakTagApiSlaTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly RecommenderService _service;

    public WeakTagApiSlaTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RecommenderDbContext(options);
        var difficultyMapping = new DifficultyMappingService();
        _service = new RecommenderService(_db, difficultyMapping);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetStudentWeakTags_With50PlusMasteryRows_CompletesWithin2Seconds()
    {
        // ── Seed: 60 TagsMastery rows for one student ────────────────────────
        var studentId = Guid.NewGuid().ToString();
        const int totalTags = 60;
        const int weakTagCount = 30; // Half weak (< 5.00), half strong (>= 5.00)

        for (int i = 0; i < totalTags; i++)
        {
            var tagId = Guid.NewGuid().ToString();

            // Seed the TagTopic (read-only cross-module data)
            _db.TagTopics.Add(new TagTopicReadOnly
            {
                TagId = tagId,
                TagName = $"Topic_{i:D3}",
                Grade = 10,
                IsActive = true
            });

            // Seed TagsMastery: first half weak, second half strong
            decimal officialPoint = i < weakTagCount
                ? Math.Round(0.50m + (i * 0.13m), 2)  // 0.50..4.37 (all < 5.00)
                : Math.Round(5.00m + ((i - weakTagCount) * 0.15m), 2); // 5.00..9.50

            _db.TagsMasteries.Add(new TagsMastery
            {
                TagsMasteryId = Guid.NewGuid().ToString(),
                StudentId = studentId,
                TagId = tagId,
                OfficialPoint = officialPoint,
                PracticePoint = 5.00m,
                ExamAnchor = officialPoint,
                MasteryStatus = officialPoint >= 7.50m ? "Mastered" : officialPoint < 5.00m ? "Learning" : "Learning",
                NumberDone = 10,
                SeriesAnswerCount = 0,
                RecommendedDifficultyLevel = officialPoint switch
                {
                    < 3.00m => 1,
                    < 5.00m => 2,
                    < 7.50m => 3,
                    _ => 4
                },
                ExamHistory = "[]"
            });
        }

        await _db.SaveChangesAsync();

        // ── Act: Time the weak tags query ────────────────────────────────────
        var sw = Stopwatch.StartNew();
        var weakTags = await _service.GetStudentWeakTagsAsync(studentId);
        sw.Stop();

        // ── Assert ──────────────────────────────────────────────────────────
        // G4 SLA: < 2 seconds
        Assert.True(sw.Elapsed.TotalSeconds < 2.0,
            $"WeakTag query took {sw.Elapsed.TotalSeconds:F3}s, exceeds 2.0s SLA");

        // Verify correct count of weak tags returned
        Assert.Equal(weakTagCount, weakTags.Count);

        // Verify all returned tags are actually weak (official_point < 5.00)
        Assert.All(weakTags, wt => Assert.True(wt.OfficialPoint < 5.00m,
            $"Tag {wt.TagName} has OfficialPoint={wt.OfficialPoint}, should be < 5.00"));

        // Verify ordering: ascending by OfficialPoint
        for (int i = 1; i < weakTags.Count; i++)
        {
            Assert.True(weakTags[i].OfficialPoint >= weakTags[i - 1].OfficialPoint,
                $"WeakTags not sorted ascending: [{i-1}]={weakTags[i-1].OfficialPoint}, [{i}]={weakTags[i].OfficialPoint}");
        }
    }
}

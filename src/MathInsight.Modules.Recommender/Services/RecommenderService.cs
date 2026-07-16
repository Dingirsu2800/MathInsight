using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Shared.Recommendation;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// SQL-only implementation of <see cref="IRecommenderService"/> for MVP.
/// Reads TagsMastery and joins to TagTopic (read-only) to resolve tag names.
/// Redis cache is optional for future optimization.
/// </summary>
public sealed class RecommenderService : IRecommenderService, IStudentRecommendationProvider
{
    private const decimal WeakThreshold = 5.00m;

    private readonly RecommenderDbContext _db;
    private readonly IDifficultyMappingService _difficultyMapping;

    public RecommenderService(RecommenderDbContext db, IDifficultyMappingService difficultyMapping)
    {
        _db = db;
        _difficultyMapping = difficultyMapping;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeakTagDto>> GetStudentWeakTagsAsync(
        string studentId, CancellationToken cancellationToken = default)
    {
        // RCM-03: WeakTag = official_point < 5.00.
        // No-row behavior (MVP): Topics without a TagsMastery row are NOT returned as weak.
        var weakTags = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on tm.TagId equals tt.TagId
            where tm.StudentId == studentId && tm.OfficialPoint < WeakThreshold && tt.IsActive
            orderby tm.OfficialPoint ascending
            select new WeakTagDto(
                tm.TagId,
                tt.TagName,
                tm.OfficialPoint)
        ).ToListAsync(cancellationToken);

        return weakTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId, CancellationToken cancellationToken = default)
    {
        var masteryRows = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on tm.TagId equals tt.TagId
            where tm.StudentId == studentId && tm.OfficialPoint < WeakThreshold && tt.IsActive
            orderby tm.OfficialPoint ascending
            select new
            {
                tm.TagId,
                tt.TagName,
                tm.OfficialPoint,
                tm.RecommendedDifficultyLevel
            }
        ).ToListAsync(cancellationToken);

        var result = masteryRows.Select(row =>
        {
            bool isWeak = _difficultyMapping.IsWeak(row.OfficialPoint);
            bool isRemedial = _difficultyMapping.IsRemedial(row.RecommendedDifficultyLevel, row.OfficialPoint);

            string reason = isRemedial
                ? "RemedialLevel1"
                : isWeak
                    ? "OfficialPointBelow5"
                    : "NormalPractice";

            return new WeakTagAdvice(
                row.TagId,
                row.TagName,
                row.OfficialPoint,
                IsWeak: isWeak,
                RecommendedDifficultyLevel: row.RecommendedDifficultyLevel,
                IsRemedial: isRemedial,
                Reason: reason);
        }).ToList();

        return result;
    }
}

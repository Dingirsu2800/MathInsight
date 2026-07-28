using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Persistence;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// SQL-only implementation of <see cref="IRecommenderService"/> for MVP.
/// Reads TagsMastery and joins to TagTopic (read-only) to resolve tag names.
/// Resolves TagDifficulty.DifficultyID based on RecommendedDifficultyLevel (1..4).
/// </summary>
public sealed class RecommenderService : IRecommenderService
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
        var weakTags = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on tm.TagId equals tt.TagId
            where tm.StudentId == studentId && tm.OfficialPoint < WeakThreshold
            orderby tm.OfficialPoint ascending
            select new WeakTagDto(
                tm.TagId,
                tt.TagName,
                tm.OfficialPoint,
                tm.NumberDone)
        ).ToListAsync(cancellationToken);

        return weakTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(
        string studentId, CancellationToken cancellationToken = default)
    {
        var difficulties = await _db.TagDifficulties
            .AsNoTracking()
            .ToDictionaryAsync(td => td.LevelValue, td => td.DifficultyId, cancellationToken);

        var masteryRows = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on tm.TagId equals tt.TagId
            where tm.StudentId == studentId && tm.OfficialPoint < WeakThreshold
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
            bool isBottleneckWeak = _difficultyMapping.IsBottleneckWeak(row.OfficialPoint);

            string reason = isBottleneckWeak
                ? "BottleneckSubTag"
                : isRemedial
                    ? "RemedialLevel1"
                    : isWeak
                        ? "OfficialPointBelow5"
                        : "NormalPractice";

            difficulties.TryGetValue(row.RecommendedDifficultyLevel, out var difficultyId);

            return new WeakTagAdviceDto(
                row.TagId,
                row.TagName,
                row.OfficialPoint,
                IsWeak: isWeak,
                RecommendedDifficultyLevel: row.RecommendedDifficultyLevel,
                IsRemedial: isRemedial,
                Reason: reason,
                RecommendedDifficultyId: difficultyId);
        }).ToList();

        return result;
    }
}

using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// SQL-only implementation of <see cref="IRecommenderService"/> for MVP.
/// Reads TagsMastery and joins to TagTopic (read-only) to resolve tag names.
/// Resolves TagDifficulty.DifficultyID based on RecommendedDifficultyLevel (1..4).
/// </summary>
public sealed class RecommenderService : IRecommenderService, IStudentRecommendationProvider, IStudentTopicMasteryProvider
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
            join parent in _db.TagTopics.AsNoTracking() on tt.ParentTagId equals parent.TagId
            where tm.StudentId == studentId
                && tm.OfficialPoint < WeakThreshold
                && tt.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == tt.Grade
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
    public async Task<IReadOnlyList<TagMasteryDto>> GetStudentAllTagsMasteryAsync(
        string studentId, CancellationToken cancellationToken = default)
    {
        var allMastery = await (
            from tt in _db.TagTopics.AsNoTracking()
            join parent in _db.TagTopics.AsNoTracking() on tt.ParentTagId equals parent.TagId
            join tm in _db.TagsMasteries.AsNoTracking().Where(m => m.StudentId == studentId)
                on tt.TagId equals tm.TagId into tmGroup
            from tm in tmGroup.DefaultIfEmpty()
            where tt.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == tt.Grade
            orderby (tm != null ? tm.OfficialPoint : 3.27m) ascending, tt.TagId ascending
            select new TagMasteryDto(
                tt.TagId,
                tt.TagName,
                tm != null ? tm.OfficialPoint : 3.27m,
                tm != null ? tm.NumberDone : 0,
                tm != null ? tm.MasteryStatus : "NotLearned",
                tm != null ? tm.RecommendedDifficultyLevel : (byte)1,
                tt.Grade)
        ).ToListAsync(cancellationToken);

        return allMastery;
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
            join parent in _db.TagTopics.AsNoTracking() on tt.ParentTagId equals parent.TagId
            where tm.StudentId == studentId
                && tm.OfficialPoint < WeakThreshold
                && tt.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == tt.Grade
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

    public async Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from mastery in _db.TagsMasteries.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on mastery.TagId equals topic.TagId
            join parent in _db.TagTopics.AsNoTracking() on topic.ParentTagId equals parent.TagId
            where mastery.StudentId == studentId
                && mastery.OfficialPoint < WeakThreshold
                && mastery.NumberDone >= 3
                && topic.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == topic.Grade
            orderby mastery.OfficialPoint, mastery.TagId
            select new WeakTagAdvice(
                mastery.TagId,
                topic.TagName,
                mastery.OfficialPoint,
                mastery.NumberDone,
                mastery.RecommendedDifficultyLevel,
                mastery.OfficialPoint < 4.00m ? "BottleneckSubTag" : "OfficialPointBelow5"))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, TopicMasteryAdvice>> GetTopicMasteryAdviceAsync(
        string studentId,
        IReadOnlyCollection<string> tagIds,
        CancellationToken cancellationToken = default)
    {
        var requestedTagIds = tagIds
            .Where(tagId => !string.IsNullOrWhiteSpace(tagId))
            .Select(tagId => tagId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (string.IsNullOrWhiteSpace(studentId) || requestedTagIds.Count == 0)
            return new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase);

        var rows = await (
            from mastery in _db.TagsMasteries.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on mastery.TagId equals topic.TagId
            join parent in _db.TagTopics.AsNoTracking() on topic.ParentTagId equals parent.TagId
            where mastery.StudentId == studentId
                && requestedTagIds.Contains(mastery.TagId)
                && topic.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == topic.Grade
                && mastery.RecommendedDifficultyLevel >= 1
                && mastery.RecommendedDifficultyLevel <= 4
            select new
            {
                mastery.TagId,
                mastery.OfficialPoint,
                mastery.NumberDone,
                mastery.RecommendedDifficultyLevel
            })
            .ToListAsync(cancellationToken);

        var sessionCountRows = await _db.StudentTopicSessionResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && requestedTagIds.Contains(result.TagId)
                && result.TotalItems > 0)
            .GroupBy(result => result.TagId)
            .Select(group => new
            {
                TagId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var sessionCounts = sessionCountRows
            .GroupBy(row => row.TagId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.Count),
                StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            sessionCounts.TryGetValue(row.TagId, out var sessionCount);
            result[row.TagId] = new TopicMasteryAdvice(
                row.TagId,
                Math.Clamp(row.OfficialPoint, 0m, 10m),
                Math.Max(0, row.NumberDone),
                sessionCount,
                row.RecommendedDifficultyLevel);
        }

        return result;
    }
}

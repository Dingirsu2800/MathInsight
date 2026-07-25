using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Services;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// Handles <see cref="GetRecommendedLecturesQuery"/>: matches Lecture.TagID to student's
/// weak TagIDs (official_point &lt; 5.00). Remedial topics sorted first (UC-53, RCM-10).
/// </summary>
public sealed class GetRecommendedLecturesQueryHandler
    : IRequestHandler<GetRecommendedLecturesQuery, IReadOnlyList<RecommendedLectureResponse>>
{
    private const decimal WeakThreshold = 5.00m;

    private readonly RecommenderDbContext _db;
    private readonly IDifficultyMappingService _difficultyMapping;

    public GetRecommendedLecturesQueryHandler(
        RecommenderDbContext db,
        IDifficultyMappingService difficultyMapping)
    {
        _db = db;
        _difficultyMapping = difficultyMapping;
    }

    public async Task<IReadOnlyList<RecommendedLectureResponse>> Handle(
        GetRecommendedLecturesQuery request, CancellationToken cancellationToken)
    {
        // Step 1: Get weak tag IDs with mastery data
        var weakTags = await _db.TagsMasteries
            .AsNoTracking()
            .Where(tm => tm.StudentId == request.StudentId && tm.OfficialPoint < WeakThreshold)
            .Select(tm => new
            {
                tm.TagId,
                tm.OfficialPoint,
                tm.RecommendedDifficultyLevel
            })
            .ToListAsync(cancellationToken);

        if (weakTags.Count == 0)
            return [];

        var weakTagIds = weakTags.Select(wt => wt.TagId).ToHashSet();

        // Step 2: Join lectures to weak tags, resolve tag names
        var lectures = await (
            from l in _db.Lectures.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on l.TagId equals tt.TagId
            where weakTagIds.Contains(l.TagId) && l.Status == "Published"
            select new
            {
                l.LectureId,
                l.Title,
                l.Description,
                l.TagId,
                tt.TagName
            }
        ).ToListAsync(cancellationToken);

        // Step 3: Enrich with mastery data and sort remedial first
        var weakTagLookup = weakTags.ToDictionary(wt => wt.TagId);

        var result = lectures
            .Select(lec =>
            {
                var wt = weakTagLookup[lec.TagId];
                bool isRemedial = _difficultyMapping.IsRemedial(
                    wt.RecommendedDifficultyLevel, wt.OfficialPoint);

                return new RecommendedLectureResponse(
                    lec.LectureId,
                    lec.Title,
                    lec.Description,
                    lec.TagId,
                    lec.TagName,
                    wt.OfficialPoint,
                    isRemedial);
            })
            .OrderByDescending(r => r.IsRemedial)   // remedial first
            .ThenBy(r => r.OfficialPoint)             // worst scores first
            .ToList();

        return result;
    }
}

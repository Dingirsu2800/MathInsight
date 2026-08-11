using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Services;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;

/// <summary>
/// Handles <see cref="GetRecommendedMaterialsQuery"/>: matches materials through
/// LectureMaterial → Lecture.TagID to student's weak TagIDs.
/// Remedial topics sorted first (UC-54, RCM-10).
/// </summary>
public sealed class GetRecommendedMaterialsQueryHandler
    : IRequestHandler<GetRecommendedMaterialsQuery, IReadOnlyList<RecommendedMaterialResponse>>
{
    private const decimal WeakThreshold = 5.00m;

    private readonly RecommenderDbContext _db;
    private readonly IDifficultyMappingService _difficultyMapping;

    public GetRecommendedMaterialsQueryHandler(
        RecommenderDbContext db,
        IDifficultyMappingService difficultyMapping)
    {
        _db = db;
        _difficultyMapping = difficultyMapping;
    }

    public async Task<IReadOnlyList<RecommendedMaterialResponse>> Handle(
        GetRecommendedMaterialsQuery request, CancellationToken cancellationToken)
    {
        // Step 1: Get weak tag IDs with mastery data
        var weakTags = await (
            from mastery in _db.TagsMasteries.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on mastery.TagId equals topic.TagId
            join parent in _db.TagTopics.AsNoTracking() on topic.ParentTagId equals parent.TagId
            where mastery.StudentId == request.StudentId
                && mastery.OfficialPoint < WeakThreshold
                && topic.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == topic.Grade
            select new
            {
                mastery.TagId,
                mastery.OfficialPoint,
                mastery.RecommendedDifficultyLevel
            })
            .ToListAsync(cancellationToken);

        if (weakTags.Count == 0)
            return [];

        var weakTagIds = weakTags.Select(wt => wt.TagId).ToHashSet();

        // Step 2: Join Material → LectureMaterial → Lecture → TagTopic
        // to find materials linked to weak tags
        var materials = await (
            from m in _db.Materials.AsNoTracking()
            join lm in _db.LectureMaterials.AsNoTracking() on m.MaterialId equals lm.MaterialId
            join l in _db.Lectures.AsNoTracking() on lm.LectureId equals l.LectureId
            join tt in _db.TagTopics.AsNoTracking() on l.TagId equals tt.TagId
            join parent in _db.TagTopics.AsNoTracking() on tt.ParentTagId equals parent.TagId
            where weakTagIds.Contains(l.TagId)
                && l.Status == "Published"
                && m.Status == "Active"
                && tt.IsActive
                && parent.IsActive
                && parent.ParentTagId == null
                && parent.Grade == tt.Grade
            select new
            {
                m.MaterialId,
                Title = m.MaterialName,
                Description = (string?)null,
                m.FileUrl,
                MaterialType = m.FileType,
                l.TagId,
                tt.TagName
            }
        ).Distinct().ToListAsync(cancellationToken);

        // Step 3: Enrich with mastery data and sort remedial first
        var weakTagLookup = weakTags.ToDictionary(wt => wt.TagId);

        var result = materials
            .Select(mat =>
            {
                var wt = weakTagLookup[mat.TagId];
                bool isRemedial = _difficultyMapping.IsRemedial(
                    wt.RecommendedDifficultyLevel, wt.OfficialPoint);

                return new RecommendedMaterialResponse(
                    mat.MaterialId,
                    mat.Title,
                    mat.Description,
                    mat.FileUrl,
                    mat.MaterialType,
                    mat.TagId,
                    mat.TagName,
                    wt.OfficialPoint,
                    isRemedial);
            })
            .OrderByDescending(r => r.IsRemedial)   // remedial first
            .ThenBy(r => r.OfficialPoint)             // worst scores first
            .ToList();

        return result;
    }
}

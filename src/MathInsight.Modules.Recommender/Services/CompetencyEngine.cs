using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// Recalculates CompetencyPoint for a student's grade level after TagsMastery changes.
///
/// RCM-12: CompetencyPoint.point = AVERAGE(official_point) of mastery rows on active,
/// directly assignable child topics in the student's current grade.
/// Upsert by unique key (student_id, grade). Clamp to [0.00, 10.00].
///
/// NOTE: Grade-to-tag mapping is derived from the Tag.Grade field read from the shared
/// QuestionBank tables. For MVP, grade is passed in from the event/caller context.
/// </summary>
public sealed class CompetencyEngine : ICompetencyEngine
{
    private readonly RecommenderDbContext _db;

    public CompetencyEngine(RecommenderDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(string studentId, int grade, CancellationToken cancellationToken = default)
    {
        // Ignore legacy root, inactive, cross-grade, and nested topics. A competency point
        // must never be inferred from mastery that does not belong to this school grade.
        var averagePoint = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on tm.TagId equals topic.TagId
            join parent in _db.TagTopics.AsNoTracking() on topic.ParentTagId equals parent.TagId
            where tm.StudentId == studentId &&
                  topic.IsActive &&
                  parent.IsActive &&
                  string.IsNullOrWhiteSpace(parent.ParentTagId) &&
                  topic.Grade == grade &&
                  parent.Grade == grade
            select (decimal?)tm.OfficialPoint
        ).AverageAsync(cancellationToken);

        if (averagePoint is null)
            return; // Keep any historical point; do not create a point from another grade.

        var point = Math.Clamp(averagePoint.Value, 0.00m, 10.00m);

        // Upsert CompetencyPoint by (student_id, grade)
        var existing = await _db.CompetencyPoints
            .FirstOrDefaultAsync(cp => cp.StudentId == studentId && cp.Grade == grade, cancellationToken);

        if (existing is null)
        {
            _db.CompetencyPoints.Add(new Persistence.Entities.CompetencyPoint
            {
                CompetencyId = Guid.NewGuid().ToString(),
                StudentId = studentId,
                Grade = grade,
                Point = point
            });
        }
        else
        {
            existing.Point = point;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

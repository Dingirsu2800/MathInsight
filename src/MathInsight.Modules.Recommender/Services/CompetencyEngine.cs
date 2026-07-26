using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// Recalculates CompetencyPoint for a student's grade level after TagsMastery changes.
///
/// RCM-12: CompetencyPoint.point = AVERAGE(official_point) of all TagsMastery rows
/// for that student where the Tag belongs to the student's grade (10, 11, or 12).
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
        // RCM-12: Query average official_point across TagsMastery rows for this student
        // where the TagTopic belongs to the specified grade level (10, 11, or 12).
        var averagePoint = await (
            from tm in _db.TagsMasteries.AsNoTracking()
            join tt in _db.TagTopics.AsNoTracking() on tm.TagId equals tt.TagId
            where tm.StudentId == studentId && tt.Grade == grade
            select (double?)tm.OfficialPoint
        ).AverageAsync(cancellationToken);

        // Fallback if no grade-matching tag topics exist: average all TagsMastery rows for student
        if (averagePoint is null)
        {
            averagePoint = await _db.TagsMasteries
                .Where(tm => tm.StudentId == studentId)
                .AverageAsync(tm => (double?)tm.OfficialPoint, cancellationToken);
        }

        if (averagePoint is null)
            return; // No TagsMastery rows yet; nothing to recalculate.

        var point = Math.Clamp((decimal)averagePoint.Value, 0.00m, 10.00m);

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

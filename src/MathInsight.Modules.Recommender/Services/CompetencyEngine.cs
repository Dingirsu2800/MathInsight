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
/// Grade-to-tag mapping is derived from TagTopic.Grade and the student's current grade.
/// </summary>
public sealed class CompetencyEngine : ICompetencyEngine
{
    private readonly RecommenderDbContext _db;

    public CompetencyEngine(RecommenderDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(string studentId, CancellationToken cancellationToken = default)
    {
        // Student is a cross-module read model; Recommender never writes to it.
        var grade = await _db.Students
            .AsNoTracking()
            .Where(student => student.StudentId == studentId)
            .Select(student => student.CurrentGrade)
            .SingleOrDefaultAsync(cancellationToken);

        if (grade is null)
            return;

        var averagePoint = await (
            from mastery in _db.TagsMasteries.AsNoTracking()
            join tag in _db.TagTopics.AsNoTracking() on mastery.TagId equals tag.TagId
            where mastery.StudentId == studentId && tag.Grade == grade.Value
            select (decimal?)mastery.OfficialPoint
        ).AverageAsync(cancellationToken);

        if (averagePoint is null)
            return;

        var point = Math.Clamp(averagePoint.Value, 0.00m, 10.00m);

        // Upsert CompetencyPoint by (student_id, grade)
        var existing = await _db.CompetencyPoints
            .FirstOrDefaultAsync(cp => cp.StudentId == studentId && cp.Grade == grade.Value, cancellationToken);

        if (existing is null)
        {
            _db.CompetencyPoints.Add(new Persistence.Entities.CompetencyPoint
            {
                CompetencyId = Guid.NewGuid().ToString("D"),
                StudentId = studentId,
                Grade = grade.Value,
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

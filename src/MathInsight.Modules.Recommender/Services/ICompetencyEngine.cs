namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// Recalculates the student's overall CompetencyPoint after TagsMastery changes.
/// Called by TopicResultIngestionHandler after each TagsMastery upsert (RCM-12).
/// </summary>
public interface ICompetencyEngine
{
    /// <summary>
    /// Recalculates and upserts CompetencyPoint for the given student and grade.
    /// Formula: AVERAGE(TagsMastery.official_point) for all tags belonging to that grade.
    /// Result is clamped to [0.00, 10.00].
    /// </summary>
    Task RecalculateAsync(Guid studentId, int grade, CancellationToken cancellationToken = default);
}

namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Tracks overall competency score per student per school grade.
/// Owned by Recommender module. Maps to DB script table: CompetencyPoint.
/// Unique constraint: (student_id, grade).
/// </summary>
public class CompetencyPoint
{
    public Guid CompetencyId { get; set; }
    public Guid StudentId { get; set; }

    /// <summary>Grade level: 10, 11, or 12.</summary>
    public int Grade { get; set; }

    /// <summary>Overall competency score in range 0.00..10.00.</summary>
    public decimal Point { get; set; }
}

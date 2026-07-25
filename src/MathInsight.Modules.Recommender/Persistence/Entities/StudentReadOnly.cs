namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only view of the Student table (owned by Identity_Access).
/// Used by Recommender to resolve the student's grade (G2).
/// </summary>
public class StudentReadOnly
{
    public string StudentId { get; set; } = string.Empty;
    public int? CurrentGrade { get; set; }
}

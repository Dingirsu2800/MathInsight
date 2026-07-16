namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Cross-module read model used only to resolve the student's current grade.
/// </summary>
public sealed class StudentReadOnly
{
    public string StudentId { get; set; } = string.Empty;
    public int? CurrentGrade { get; set; }
}

namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity for COMPOSITE question parts — owned by Testing/QuestionBank.
/// Stores student answer per statement/part; Grading writes IsCorrect and PointsEarned per part.
/// </summary>
public class TestAnswerPart
{
    public Guid TestAnswerPartId { get; set; }
    public Guid TestAnswerId { get; set; }
    public Guid QuestionPartId { get; set; }
    public string? StudentAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal PointsEarned { get; set; }

    // Navigation
    public TestAnswer TestAnswer { get; set; } = null!;
    public QuestionPart QuestionPart { get; set; } = null!;
}

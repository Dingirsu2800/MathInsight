namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — composite PK (TestAnswerId, AnswerId).
/// Used by Grading for MULTIPLE_SELECT scoring: compare selected set to correct set.
/// </summary>
public class TestAnswerOption
{
    public Guid TestAnswerId { get; set; }
    public Guid AnswerId { get; set; }

    // Navigation
    public TestAnswer TestAnswer { get; set; } = null!;
    public Answer Answer { get; set; } = null!;
}

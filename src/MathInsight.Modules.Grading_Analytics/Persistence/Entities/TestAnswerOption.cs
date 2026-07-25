namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — composite PK (TestAnswerId, AnswerId).
/// Used by Grading for MULTIPLE_SELECT scoring: compare selected set to correct set.
/// </summary>
public class TestAnswerOption
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string AnswerId { get; set; } = string.Empty;

    // Navigation
    public TestAnswer TestAnswer { get; set; } = null!;
    public Answer Answer { get; set; } = null!;
}

namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by Testing module (003).
/// Grading reads answers and writes: IsCorrect, PointsEarned.
/// </summary>
public class TestAnswer
{
    public Guid TestAnswerId { get; set; }
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }

    /// <summary>Nullable for MultipleSelect and ShortAnswer (options in TestAnswerOption)</summary>
    public Guid? AnswerId { get; set; }

    public int QuestionNo { get; set; }
    public int? TimeSpent { get; set; }
    public DateTime? FirstChoiceTime { get; set; }
    public DateTime? UpdateChoiceTime { get; set; }
    public string? ShortAnswerText { get; set; }

    /// <summary>Set by Grading; null until graded</summary>
    public bool? IsCorrect { get; set; }

    /// <summary>Set by Grading; 0.00 until graded</summary>
    public decimal PointsEarned { get; set; }

    // Navigation
    public TestSession Session { get; set; } = null!;
    public Question Question { get; set; } = null!;

    /// <summary>Scoring snapshot for this question within the test. Provides MaxPointsSnapshot, ScoringRuleSnapshot, IsScoreInvalidated.</summary>
    public TestQuestion? TestQuestion { get; set; }

    public ICollection<TestAnswerOption> SelectedOptions { get; set; } = new List<TestAnswerOption>();
    public ICollection<TestAnswerPart> AnswerParts { get; set; } = new List<TestAnswerPart>();
}

namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by Testing module (003).
/// Grading reads answers and writes: IsCorrect, PointsEarned.
/// </summary>
public class TestAnswer
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>Nullable for MultipleSelect and ShortAnswer (options in TestAnswerOption)</summary>
    public string? AnswerId { get; set; }

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
    public ICollection<TestAnswerOption> SelectedOptions { get; set; } = new List<TestAnswerOption>();
    public ICollection<TestAnswerPart> AnswerParts { get; set; } = new List<TestAnswerPart>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public MathInsight.Shared.Questions.QuestionSnapshotV2? Snapshot { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MaxPointsSnapshot { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ScoringRuleSnapshot { get; set; } = "AllOrNothing";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsScoreInvalidated { get; set; }
}

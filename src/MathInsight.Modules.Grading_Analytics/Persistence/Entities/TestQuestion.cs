namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by Testing module (003).
/// Stores scoring snapshot at Test generation time.
/// </summary>
public class TestQuestion
{
    public string TestId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>The exact QuestionVersion used when this Test was generated.</summary>
    public string? QuestionVersionId { get; set; }

    /// <summary>Question weight snapshot at generation time.</summary>
    public decimal WeightSnapshot { get; set; }

    /// <summary>Maximum points this question is worth in this Test.</summary>
    public decimal MaxPointsSnapshot { get; set; }

    /// <summary>AllOrNothing | TieredTrueFalse | WeightedParts — scoring rule at generation time.</summary>
    public string? ScoringRuleSnapshot { get; set; }

    /// <summary>True when the question has been confirmed as erroneous after a report.</summary>
    public bool IsScoreInvalidated { get; set; }

    /// <summary>The ReportID that caused score invalidation, if any.</summary>
    public string? InvalidatedByReportId { get; set; }

    // Navigation
    public Question Question { get; set; } = null!;
    public QuestionVersion? QuestionVersion { get; set; }
}

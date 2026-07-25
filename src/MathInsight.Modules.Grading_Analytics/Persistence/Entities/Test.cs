namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by Testing module (003).
/// Grading reads MaxScore and ScoringPolicy to compute the final session score.
/// </summary>
public class Test
{
    public Guid TestId { get; set; }

    /// <summary>Total score for this test, e.g. 10.00.</summary>
    public decimal MaxScore { get; set; }

    /// <summary>BlueprintBudget | NormalizedWeight — determines how session score is computed.</summary>
    public string? ScoringPolicy { get; set; }
}

namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

public sealed class TestQuestion
{
    public string TestId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionVersionId { get; set; } = string.Empty;
    public decimal WeightSnapshot { get; set; }
    public decimal MaxPointsSnapshot { get; set; }
    public string ScoringRuleSnapshot { get; set; } = "AllOrNothing";
    public bool IsScoreInvalidated { get; set; }
    public string? InvalidatedByReportId { get; set; }
    public QuestionVersion QuestionVersion { get; set; } = null!;
}

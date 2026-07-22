namespace MathInsight.Modules.Testing.Entities;

public class TestQuestion
{
    public string TestId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int QuestionOrder { get; set; }
    public string? SourceBlueprintDetailId { get; set; }
    public string SelectionReason { get; set; } = "BlueprintNormal";
    public bool IsAdaptiveSelected { get; set; }
    public string? RecommendedForTagId { get; set; }
    public string? RecommendedDifficultyId { get; set; }
    public decimal? PtagAtSelection { get; set; }
    public string? RuleVersion { get; set; }
    public string QuestionVersionId { get; set; } = string.Empty;
    public decimal WeightSnapshot { get; set; }
    public decimal MaxPointsSnapshot { get; set; }
    public string ScoringRuleSnapshot { get; set; } = "AllOrNothing";
    public bool IsScoreInvalidated { get; set; }
    public string? InvalidatedByReportId { get; set; }

    // Navigation
    public Test? Test { get; set; }
    public QuestionVersion? QuestionVersion { get; set; }
}

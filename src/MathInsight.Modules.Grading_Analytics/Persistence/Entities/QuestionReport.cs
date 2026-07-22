namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

public sealed class QuestionReport
{
    public string ReportId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string ReporterRole { get; set; } = string.Empty;
    public string ReportReason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? QuestionVersionId { get; set; }
    public string? ResolutionAction { get; set; }
    public DateTime? ScoreAdjustedTime { get; set; }
}

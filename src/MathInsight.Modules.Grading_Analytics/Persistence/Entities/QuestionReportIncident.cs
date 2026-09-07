namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>Read-only projection of a QuestionBank incident used to gate score adjustments.</summary>
public sealed class QuestionReportIncident
{
    public string IncidentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiresAdminReview { get; set; }
    public string? ApprovedResolutionAction { get; set; }
    public string? AdjustmentStatus { get; set; }
}

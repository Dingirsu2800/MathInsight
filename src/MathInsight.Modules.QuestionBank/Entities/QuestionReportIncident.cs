namespace MathInsight.Modules.QuestionBank.Entities;

public sealed class QuestionReportIncident
{
    public string IncidentId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionVersionId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string Status { get; set; } = "Open";
    public bool RequiresAdminReview { get; set; }
    public string? AssignedAdminId { get; set; }
    public string? SubmittedCorrectionVersionId { get; set; }
    public string? ProposedResolutionAction { get; set; }
    public string? ApprovedResolutionAction { get; set; }
    public string? AdjustmentStatus { get; set; }
    public string? SubmissionKey { get; set; }
    public string? SubmissionPayloadHash { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }

    public Question Question { get; set; } = null!;
    public QuestionVersion QuestionVersion { get; set; } = null!;
    public ICollection<QuestionReport> Reports { get; set; } = new List<QuestionReport>();
}

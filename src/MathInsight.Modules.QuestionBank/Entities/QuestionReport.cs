namespace MathInsight.Modules.QuestionBank.Entities;

public class QuestionReport
{
    public string ReportId { get; set; } = default!;
    public string QuestionId { get; set; } = default!;
    public string ReporterAccountId { get; set; } = default!;
    public string ReporterRole { get; set; } = default!;
    public string ReportReason { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedTime { get; set; }
    public DateTime? ResolvedTime { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? SubmittedTime { get; set; }
    public DateTime? ReviewedTime { get; set; }
    public string? ReviewedBy { get; set; }

    public Question Question { get; set; } = default!;
}

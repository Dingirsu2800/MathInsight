using System;

namespace MathInsight.Modules.Learning_Lecture.Entities;

public class DiscussionReport
{
    public string ReportId { get; set; } = default!;
    public string? DiscussionQuestionId { get; set; }
    public string? DiscussionAnswerId { get; set; }
    public string ReporterAccountId { get; set; } = default!;
    public string ReportReason { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedTime { get; set; }
    public DateTime? ResolvedTime { get; set; }
    public string? ResolverAccountId { get; set; }

    public DiscussionQuestion? Question { get; set; }
    public DiscussionAnswer? Answer { get; set; }
}

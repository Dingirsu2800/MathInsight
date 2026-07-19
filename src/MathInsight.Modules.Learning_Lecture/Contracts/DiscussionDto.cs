using System;
using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Contracts;

public class DiscussionQuestionDto
{
    public string DiscussionQuestionId { get; set; } = default!;
    public string LectureId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public string AuthorName { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<DiscussionAnswerDto> Answers { get; set; } = new();
}

public class DiscussionAnswerDto
{
    public string DiscussionAnswerId { get; set; } = default!;
    public string AccountId { get; set; } = default!;
    public string AuthorName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

public class DiscussionReportDto
{
    public string ReportId { get; set; } = default!;
    public string? DiscussionQuestionId { get; set; }
    public string? DiscussionAnswerId { get; set; }
    public string ReporterAccountId { get; set; } = default!;
    public string ReporterName { get; set; } = default!;
    public string ReportReason { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedTime { get; set; }
    public string TargetType { get; set; } = default!;
    public string TargetAuthorName { get; set; } = default!;
    public string TargetPreview { get; set; } = default!;
    public string LectureTitle { get; set; } = default!;
    public string LectureId { get; set; } = default!;
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

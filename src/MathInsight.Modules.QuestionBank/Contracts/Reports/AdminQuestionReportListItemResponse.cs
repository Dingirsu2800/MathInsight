namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record AdminQuestionReportListItemResponse(
    string ReportId,
    string QuestionId,
    string QuestionContent,
    string QuestionStatus,
    string ExpertId,
    string? ExpertName,
    string ReportReason,
    string? ReviewNote,
    string Status,
    DateTime CreatedTime,
    DateTime? SubmittedTime,
    DateTime? ReviewedTime,
    string? ReviewedBy);

namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record QuestionReportResponse(
    string ReportId,
    string QuestionId,
    string ReporterAccountId,
    string? ReporterName,
    string ReporterRole,
    string ReportReason,
    string Status,
    DateTime CreatedTime,
    DateTime? ResolvedTime,
    string? ResolvedBy,
    string? ReviewNote,
    DateTime? SubmittedTime,
    DateTime? ReviewedTime,
    string? ReviewedBy,
    string? SessionId,
    string? QuestionVersionId,
    string? ResolutionAction,
    DateTime? ScoreAdjustedTime);

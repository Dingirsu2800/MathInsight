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
    string? ResolvedBy);

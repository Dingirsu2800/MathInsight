namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record ReportQuestionResponse(
    string ReportId,
    string QuestionId,
    string ReporterRole,
    string ReportReason,
    string Status,
    DateTime CreatedTime,
    string QuestionStatus,
    bool QuestionIsActive);

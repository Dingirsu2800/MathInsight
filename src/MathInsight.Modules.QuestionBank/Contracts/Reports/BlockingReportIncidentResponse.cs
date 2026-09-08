namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record BlockingReportIncidentResponse(
    string IncidentId,
    string QuestionVersionId,
    string Status,
    bool RequiresAdminReview);

namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record ReportEligibilityResponse(
    bool CanReport,
    string? ReasonCode,
    string? MyReportId,
    string? MyReportStatus,
    string? IncidentId,
    string? IncidentStatus,
    string? QuestionVersionId,
    bool RequiresAdminReview = false,
    string? ResolutionAction = null,
    string? AdjustmentStatus = null);

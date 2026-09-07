using MathInsight.Modules.QuestionBank.Contracts.Questions;

namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record QuestionReportIncidentDetailResponse(
    string IncidentId,
    string QuestionId,
    int Revision,
    string Status,
    bool RequiresAdminReview,
    string? AssignedAdminId,
    string? SubmittedCorrectionVersionId,
    string? ProposedResolutionAction,
    string? ApprovedResolutionAction,
    string? AdjustmentStatus,
    QuestionVersionResponse OriginalVersion,
    QuestionVersionResponse? SubmittedCorrectionVersion,
    IReadOnlyList<QuestionReportIncidentReportResponse> Reports);

public sealed record QuestionReportIncidentReportResponse(
    string ReportId,
    string ReporterAccountId,
    string? ReporterName,
    string ReporterRole,
    string ReportReason,
    string Status,
    string? ProposedStatus,
    string? ProposedReviewNote,
    string? ReviewNote,
    string? QuestionVersionId,
    string? ResolutionAction,
    string? SessionId,
    DateTime CreatedTime,
    DateTime? SubmittedTime,
    DateTime? ReviewedTime,
    DateTime? ResolvedTime,
    DateTime? ScoreAdjustedTime);

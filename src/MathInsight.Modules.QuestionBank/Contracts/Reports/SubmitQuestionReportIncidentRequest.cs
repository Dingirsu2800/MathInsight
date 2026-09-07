using MathInsight.Modules.QuestionBank.Contracts.Questions;

namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed class SubmitQuestionReportIncidentRequest
{
    public int ExpectedRevision { get; set; }
    public string ExpectedQuestionVersionId { get; set; } = string.Empty;
    public string SubmissionKey { get; set; } = string.Empty;
    public string ResolutionAction { get; set; } = "NoScoreChange";
    public List<QuestionReportDecisionRequest> ReportDecisions { get; set; } = [];
    public UpdateQuestionRequest Correction { get; set; } = new();
}

public sealed class QuestionReportDecisionRequest
{
    public string ReportId { get; set; } = string.Empty;
    public string Disposition { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}

public sealed record QuestionReportIncidentResponse(
    string IncidentId,
    string QuestionId,
    string QuestionVersionId,
    int Revision,
    string Status,
    bool RequiresAdminReview,
    string? SubmittedCorrectionVersionId,
    string? ProposedResolutionAction,
    string? AdjustmentStatus);

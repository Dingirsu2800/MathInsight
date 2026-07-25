using MathInsight.Modules.QuestionBank.Contracts.Questions;

namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed record ReportedQuestionListItemResponse(
    string QuestionId,
    string QuestionContent,
    int Grade,
    string Status,
    string QuestionType,
    IReadOnlyList<QuestionTopicSummaryResponse> Topics,
    int PendingReportCount,
    string LatestReportReason,
    DateTime LatestReportAt,
    IReadOnlyList<string> ReporterRoles,
    IReadOnlyList<string> ActiveReportStatuses);

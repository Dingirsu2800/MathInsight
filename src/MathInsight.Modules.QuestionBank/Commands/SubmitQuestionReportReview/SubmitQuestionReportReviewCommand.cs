using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;

public sealed record SubmitQuestionReportReviewCommand(
    string ReportId,
    string ExpertAccountId) : IRequest<Result<QuestionReportResponse>>;

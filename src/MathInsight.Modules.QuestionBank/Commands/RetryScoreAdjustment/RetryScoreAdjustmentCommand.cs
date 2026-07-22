using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.RetryScoreAdjustment;

public sealed record RetryScoreAdjustmentCommand(
    string ReportId,
    string ExpertAccountId) : IRequest<Result<QuestionReportResponse>>;

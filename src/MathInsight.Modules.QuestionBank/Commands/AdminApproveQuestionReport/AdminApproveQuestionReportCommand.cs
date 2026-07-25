using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;

public sealed record AdminApproveQuestionReportCommand(
    string ReportId,
    string AdminAccountId) : IRequest<Result<QuestionReportResponse>>;

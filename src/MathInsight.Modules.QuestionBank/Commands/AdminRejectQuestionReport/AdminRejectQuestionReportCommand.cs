using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;

public sealed record AdminRejectQuestionReportCommand(
    string ReportId,
    AdminRejectQuestionReportRequest Request,
    string AdminAccountId) : IRequest<Result<QuestionReportResponse>>;

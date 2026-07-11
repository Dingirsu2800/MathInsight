using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;

public sealed record HandleQuestionReportCommand(
    string ReportId,
    HandleQuestionReportRequest Request,
    string ExpertAccountId) : IRequest<Result<QuestionReportResponse>>;

using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.ReportQuestion;

public sealed record ReportQuestionCommand(
    string QuestionId,
    ReportQuestionRequest Request,
    string ReporterAccountId,
    string ReporterRole) : IRequest<Result<ReportQuestionResponse>>;

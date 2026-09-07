using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportIncident;

public sealed record SubmitQuestionReportIncidentCommand(
    string IncidentId,
    SubmitQuestionReportIncidentRequest Request,
    string ExpertAccountId) : IRequest<Result<QuestionReportIncidentResponse>>;

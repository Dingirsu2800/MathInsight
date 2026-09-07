using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionReportIncident;

public sealed record GetQuestionReportIncidentQuery(
    string IncidentId,
    string RequestingAccountId,
    string RequestingRole) : IRequest<Result<QuestionReportIncidentDetailResponse>>;

using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;

public sealed record GetQuestionReportsQuery(
    string QuestionId,
    string RequestingExpertId,
    string? Status) : IRequest<Result<IReadOnlyList<QuestionReportResponse>>>;

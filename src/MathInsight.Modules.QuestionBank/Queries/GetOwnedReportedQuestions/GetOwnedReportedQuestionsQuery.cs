using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetOwnedReportedQuestions;

public sealed record GetOwnedReportedQuestionsQuery(
    string OwnerExpertId,
    string? Status,
    int PageIndex,
    int PageSize) : IRequest<Result<PagedResponse<ReportedQuestionListItemResponse>>>;

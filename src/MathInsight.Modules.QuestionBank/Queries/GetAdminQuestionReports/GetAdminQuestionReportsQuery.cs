using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetAdminQuestionReports;

public sealed record GetAdminQuestionReportsQuery(
    string AdminAccountId,
    string? Status,
    int PageIndex,
    int PageSize) : IRequest<Result<PagedResponse<AdminQuestionReportListItemResponse>>>;

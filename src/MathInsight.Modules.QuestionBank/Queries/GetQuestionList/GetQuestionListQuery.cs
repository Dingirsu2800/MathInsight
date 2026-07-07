using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionList;

public sealed record GetQuestionListQuery(
    int PageIndex,
    int PageSize,
    string? Status,
    int? Grade,
    string? TagId,
    string? DifficultyId,
    string? QuestionType,
    string? ExpertId) : IRequest<Result<PagedResponse<QuestionListItemResponse>>>;

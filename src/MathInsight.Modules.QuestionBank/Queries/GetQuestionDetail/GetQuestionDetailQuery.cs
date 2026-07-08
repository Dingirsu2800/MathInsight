using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionDetail;

public sealed record GetQuestionDetailQuery(string QuestionId)
    : IRequest<Result<QuestionDetailResponse>>;

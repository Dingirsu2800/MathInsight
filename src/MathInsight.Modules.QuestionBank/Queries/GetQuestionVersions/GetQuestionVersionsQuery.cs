using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionVersions;

public sealed record GetQuestionVersionsQuery(string QuestionId)
    : IRequest<Result<IReadOnlyList<QuestionVersionResponse>>>;

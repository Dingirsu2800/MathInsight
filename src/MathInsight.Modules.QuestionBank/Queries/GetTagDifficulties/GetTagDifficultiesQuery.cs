using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;

public sealed record GetTagDifficultiesQuery() : IRequest<IReadOnlyList<TagDifficultyResponse>>;

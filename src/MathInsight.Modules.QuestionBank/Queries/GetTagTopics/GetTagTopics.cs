using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagTopics;

public sealed record GetTagTopics(int? Grade) : IRequest<IReadOnlyList<TagTopicResponse>>;

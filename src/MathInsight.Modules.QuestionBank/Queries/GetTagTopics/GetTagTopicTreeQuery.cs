using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagTopics;

public sealed record GetTagTopicTreeQuery(int? Grade) : IRequest<IReadOnlyList<TagTopicTreeResponse>>;


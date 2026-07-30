using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetTopicPracticeOptions;

public sealed record GetTopicPracticeOptionsQuery(string StudentId) : IRequest<Result<TopicPracticeOptionsResponse>>;

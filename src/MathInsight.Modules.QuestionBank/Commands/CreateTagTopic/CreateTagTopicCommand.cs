using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateTagTopic;

public sealed record CreateTagTopicCommand(
    CreateTagTopicRequest Request) : IRequest<Result<TagTopicTreeResponse>>;

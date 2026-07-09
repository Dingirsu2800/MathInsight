using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;

public sealed record UpdateTagTopicCommand(
    string TagId,
    UpdateTagTopicRequest Request) : IRequest<Result<TagTopicTreeResponse>>;

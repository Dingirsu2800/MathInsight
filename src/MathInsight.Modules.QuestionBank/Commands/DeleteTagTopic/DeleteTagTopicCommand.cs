using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteTagTopic;

public sealed record DeleteTagTopicCommand(string TagId) : IRequest<Result<DeleteTagResponse>>;

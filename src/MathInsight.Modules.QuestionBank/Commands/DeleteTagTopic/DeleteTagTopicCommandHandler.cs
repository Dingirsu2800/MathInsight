using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteTagTopic;

public sealed class DeleteTagTopicCommandHandler
    : IRequestHandler<DeleteTagTopicCommand, Result<DeleteTagResponse>>
{
    private readonly QuestionBankDbContext _context;

    public DeleteTagTopicCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DeleteTagResponse>> Handle(
        DeleteTagTopicCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TagId))
            return Result<DeleteTagResponse>.Failure(QuestionBankErrors.TagIdRequired);

        var topic = await _context.TagTopics
            .FirstOrDefaultAsync(existing => existing.TagId == command.TagId, cancellationToken);

        if (topic is null)
            return Result<DeleteTagResponse>.Failure(QuestionBankErrors.TagTopicNotFound);

        topic.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<DeleteTagResponse>.Success(
            new DeleteTagResponse(topic.TagId, "SoftDeleted", topic.IsActive));
    }
}

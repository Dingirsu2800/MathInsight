using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteTagDifficulty;

public sealed class DeleteTagDifficultyCommandHandler
    : IRequestHandler<DeleteTagDifficultyCommand, Result<DeleteTagResponse>>
{
    private readonly QuestionBankDbContext _context;

    public DeleteTagDifficultyCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DeleteTagResponse>> Handle(
        DeleteTagDifficultyCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DifficultyId))
            return Result<DeleteTagResponse>.Failure(QuestionBankErrors.TagIdRequired);

        var difficulty = await _context.TagDifficulties
            .FirstOrDefaultAsync(
                existing => existing.DifficultyId == command.DifficultyId,
                cancellationToken);

        if (difficulty is null)
            return Result<DeleteTagResponse>.Failure(QuestionBankErrors.TagDifficultyNotFound);

        difficulty.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<DeleteTagResponse>.Success(
            new DeleteTagResponse(difficulty.DifficultyId, "SoftDeleted", difficulty.IsActive));
    }
}

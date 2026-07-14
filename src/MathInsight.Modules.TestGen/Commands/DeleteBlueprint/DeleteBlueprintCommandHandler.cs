using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.DeleteBlueprint;

public sealed class DeleteBlueprintCommandHandler
    : IRequestHandler<DeleteBlueprintCommand, Result<DeleteBlueprintResponse>>
{
    private readonly TestGenDbContext _context;

    public DeleteBlueprintCommandHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DeleteBlueprintResponse>> Handle(
        DeleteBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<DeleteBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        return await BlueprintExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<DeleteBlueprintResponse>> ExecuteAsync(
        DeleteBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (BlueprintSqlServerLock.IsSupported(_context))
            await BlueprintSqlServerLock.LockAsync(_context, command.BlueprintId, cancellationToken);

        var blueprint = await _context.Blueprints.FirstOrDefaultAsync(
            item => item.BlueprintId == command.BlueprintId,
            cancellationToken);

        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.NotFound);

        if (!string.Equals(blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.MutationForbidden);

        if (blueprint.Status == BlueprintStatuses.PendingReview)
            return Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.InUse);

        var hasDirectTestReference = await _context.Tests
            .AsNoTracking()
            .AnyAsync(test => test.BlueprintId == command.BlueprintId, cancellationToken);
        var hasSourceDetailReference = await _context.BlueprintDetails
            .AsNoTracking()
            .AnyAsync(
                detail => detail.BlueprintId == command.BlueprintId && detail.TestQuestions.Any(),
                cancellationToken);

        if (blueprint.Status == BlueprintStatuses.Active ||
            hasDirectTestReference ||
            hasSourceDetailReference)
        {
            blueprint.Status = BlueprintStatuses.Deactivated;
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return Result<DeleteBlueprintResponse>.Success(
                new DeleteBlueprintResponse(
                    blueprint.BlueprintId,
                    WasDeactivated: true,
                    BlueprintStatuses.Deactivated));
        }

        if (blueprint.Status is not (
            BlueprintStatuses.Draft or
            BlueprintStatuses.Rejected or
            BlueprintStatuses.Approved))
        {
            return Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.StatusInvalid);
        }

        _context.Blueprints.Remove(blueprint);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<DeleteBlueprintResponse>.Success(
            new DeleteBlueprintResponse(
                command.BlueprintId,
                WasDeactivated: false,
                Status: null));
    }
}

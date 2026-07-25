using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.CloneBlueprint;

public sealed class CloneBlueprintCommandHandler
    : IRequestHandler<CloneBlueprintCommand, Result<CloneBlueprintResponse>>
{
    private readonly TestGenDbContext _context;

    public CloneBlueprintCommandHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CloneBlueprintResponse>> Handle(
        CloneBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<CloneBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<CloneBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var cloneBlueprintId = Guid.NewGuid().ToString();
        return await BlueprintExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, cloneBlueprintId, cancellationToken),
            () => VerifySucceededAsync(
                command,
                cloneBlueprintId,
                cancellationToken),
            cancellationToken);
    }

    private async Task<Result<CloneBlueprintResponse>> ExecuteAsync(
        CloneBlueprintCommand command,
        string cloneBlueprintId,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (BlueprintSqlServerLock.IsSupported(_context))
            await BlueprintSqlServerLock.LockAsync(_context, command.BlueprintId, cancellationToken);

        var expertExists = await _context.Experts
            .AsNoTracking()
            .AnyAsync(expert => expert.ExpertId == command.ExpertId, cancellationToken);

        if (!expertExists)
            return Result<CloneBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var source = await _context.Blueprints
            .AsNoTracking()
            .Include(blueprint => blueprint.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(
                blueprint => blueprint.BlueprintId == command.BlueprintId,
                cancellationToken);

        if (source is null || source.Status == BlueprintStatuses.Deactivated)
            return Result<CloneBlueprintResponse>.Failure(BlueprintErrors.NotFound);

        var clone = BlueprintAggregateFactory.Clone(
            source,
            command.ExpertId,
            cloneBlueprintId);
        _context.Blueprints.Add(clone);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<CloneBlueprintResponse>.Success(
            new CloneBlueprintResponse(clone.BlueprintId, clone.BlueprintName, clone.Status));
    }

    private async Task<(bool IsSuccessful, Result<CloneBlueprintResponse> Result)> VerifySucceededAsync(
        CloneBlueprintCommand command,
        string cloneBlueprintId,
        CancellationToken cancellationToken)
    {
        var persisted = await _context.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.BlueprintId == cloneBlueprintId,
                cancellationToken);
        var succeeded = persisted is not null &&
            persisted.ExpertId == command.ExpertId &&
            persisted.Status == BlueprintStatuses.Draft;

        return succeeded
            ? (true, Result<CloneBlueprintResponse>.Success(
                new CloneBlueprintResponse(
                    persisted!.BlueprintId,
                    persisted.BlueprintName,
                    persisted.Status)))
            : (false, default!);
    }
}

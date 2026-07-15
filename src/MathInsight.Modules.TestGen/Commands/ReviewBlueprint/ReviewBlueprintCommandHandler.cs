using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.ReviewBlueprint;

public sealed class ReviewBlueprintCommandHandler
    : IRequestHandler<ReviewBlueprintCommand, Result<ReviewBlueprintResponse>>
{
    private const int MaxReviewNoteLength = 2000;

    private readonly TestGenDbContext _context;

    public ReviewBlueprintCommandHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReviewBlueprintResponse>> Handle(
        ReviewBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        if (string.IsNullOrWhiteSpace(command.ReviewerExpertId))
            return Result<ReviewBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        if (command.Request is null)
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        var action = BlueprintReviewActions.Normalize(command.Request.Action);
        if (string.IsNullOrEmpty(action))
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        var reviewNote = string.IsNullOrWhiteSpace(command.Request.ReviewNote)
            ? null
            : command.Request.ReviewNote.Trim();

        if (action == BlueprintReviewActions.Reject && reviewNote is null)
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.ReviewNoteRequired);

        if (action == BlueprintReviewActions.Reject && reviewNote!.Length > MaxReviewNoteLength)
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.ReviewNoteTooLong);

        DateTime? expectedReviewTime = null;
        return await BlueprintExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(
                command,
                action,
                reviewNote,
                reviewTime => expectedReviewTime = reviewTime,
                cancellationToken),
            () => VerifySucceededAsync(
                command,
                action,
                reviewNote,
                expectedReviewTime,
                cancellationToken),
            cancellationToken);
    }

    private async Task<Result<ReviewBlueprintResponse>> ExecuteAsync(
        ReviewBlueprintCommand command,
        string action,
        string? reviewNote,
        Action<DateTime> captureReviewTime,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (BlueprintSqlServerLock.IsSupported(_context))
            await BlueprintSqlServerLock.LockAsync(_context, command.BlueprintId, cancellationToken);

        var reviewerExists = await _context.Experts
            .AsNoTracking()
            .AnyAsync(
                expert => expert.ExpertId == command.ReviewerExpertId,
                cancellationToken);

        if (!reviewerExists)
            return Result<ReviewBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var blueprint = await _context.Blueprints.FirstOrDefaultAsync(
            item => item.BlueprintId == command.BlueprintId,
            cancellationToken);

        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.NotFound);

        if (blueprint.Status != BlueprintStatuses.PendingReview)
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.StatusInvalid);

        if (string.Equals(
                blueprint.ExpertId,
                command.ReviewerExpertId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.SelfReviewForbidden);
        }

        var utcNow = DateTime.UtcNow;
        var reviewTime = new DateTime(
            utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerSecond,
            DateTimeKind.Utc);
        captureReviewTime(reviewTime);
        blueprint.Status = action == BlueprintReviewActions.Approve
            ? BlueprintStatuses.Approved
            : BlueprintStatuses.Rejected;
        blueprint.ApprovedBy = command.ReviewerExpertId;
        blueprint.ReviewNote = action == BlueprintReviewActions.Reject ? reviewNote : null;
        blueprint.ReviewTime = reviewTime;
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<ReviewBlueprintResponse>.Success(
            new ReviewBlueprintResponse(
                blueprint.BlueprintId,
                blueprint.Status,
                command.ReviewerExpertId,
                reviewTime));
    }

    private async Task<(bool IsSuccessful, Result<ReviewBlueprintResponse> Result)> VerifySucceededAsync(
        ReviewBlueprintCommand command,
        string action,
        string? reviewNote,
        DateTime? expectedReviewTime,
        CancellationToken cancellationToken)
    {
        if (expectedReviewTime is null)
            return (false, default!);

        var expectedStatus = action == BlueprintReviewActions.Approve
            ? BlueprintStatuses.Approved
            : BlueprintStatuses.Rejected;
        var expectedNote = action == BlueprintReviewActions.Reject ? reviewNote : null;
        var persisted = await _context.Blueprints
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.BlueprintId == command.BlueprintId,
                cancellationToken);
        var succeeded = persisted is not null &&
            persisted.Status == expectedStatus &&
            persisted.ApprovedBy == command.ReviewerExpertId &&
            persisted.ReviewNote == expectedNote &&
            persisted.ReviewTime == expectedReviewTime;

        return succeeded
            ? (true, Result<ReviewBlueprintResponse>.Success(
                new ReviewBlueprintResponse(
                    persisted!.BlueprintId,
                    persisted.Status,
                    command.ReviewerExpertId,
                    expectedReviewTime.Value)))
            : (false, default!);
    }
}

using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;

public sealed class SubmitBlueprintForReviewCommandHandler
    : IRequestHandler<SubmitBlueprintForReviewCommand, Result<SubmitBlueprintResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintAggregateValidator _validator;

    public SubmitBlueprintForReviewCommandHandler(
        TestGenDbContext context,
        IBlueprintAggregateValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<SubmitBlueprintResponse>> Handle(
        SubmitBlueprintForReviewCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<SubmitBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        return await BlueprintExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<SubmitBlueprintResponse>> ExecuteAsync(
        SubmitBlueprintForReviewCommand command,
        CancellationToken cancellationToken)
    {

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (BlueprintSqlServerLock.IsSupported(_context))
            await BlueprintSqlServerLock.LockAsync(_context, command.BlueprintId, cancellationToken);

        var blueprint = await _context.Blueprints
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(
                item => item.BlueprintId == command.BlueprintId,
                cancellationToken);

        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.NotFound);

        if (!string.Equals(blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.MutationForbidden);

        if (blueprint.Status is not (BlueprintStatuses.Draft or BlueprintStatuses.Rejected))
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.StatusInvalid);

        if (blueprint.DurationMinutes <= 0 ||
            blueprint.TotalQuestions <= 0 ||
            blueprint.Sections.Any(section => section.TotalQuestions <= 0))
        {
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.TotalMismatch);
        }

        var validationResult = await _validator.ValidateAsync(ToRequest(blueprint), cancellationToken);
        if (validationResult.IsFailure)
            return Result<SubmitBlueprintResponse>.Failure(validationResult.Error!);

        var sectionTotal = blueprint.Sections.Sum(section => (long)section.TotalQuestions);
        var hasDetailMismatch = blueprint.Sections.Any(section =>
            section.Details.Sum(detail => (long)detail.Quantity) != section.TotalQuestions);

        if (sectionTotal != blueprint.TotalQuestions || hasDetailMismatch)
            return Result<SubmitBlueprintResponse>.Failure(BlueprintErrors.TotalMismatch);

        blueprint.Status = BlueprintStatuses.PendingReview;
        blueprint.ApprovedBy = null;
        blueprint.ReviewNote = null;
        blueprint.ReviewTime = null;
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<SubmitBlueprintResponse>.Success(
            new SubmitBlueprintResponse(blueprint.BlueprintId, blueprint.Status));
    }

    private static BlueprintRequest ToRequest(Blueprint blueprint)
        => new()
        {
            BlueprintName = blueprint.BlueprintName,
            Grade = blueprint.Grade,
            TotalQuestions = blueprint.TotalQuestions,
            DurationMinutes = blueprint.DurationMinutes,
            Sections = blueprint.Sections
                .Select(section => new BlueprintSectionRequest
                {
                    SectionOrder = section.SectionOrder,
                    SectionCode = section.SectionCode,
                    SectionName = section.SectionName,
                    QuestionType = section.QuestionType,
                    InstructionText = section.InstructionText,
                    TotalQuestions = section.TotalQuestions,
                    DefaultPointPerQuestion = section.DefaultPointPerQuestion,
                    DefaultPointPerPart = section.DefaultPointPerPart,
                    PartCountPerQuestion = section.PartCountPerQuestion,
                    Details = section.Details
                        .Select(detail => new BlueprintDetailRequest
                        {
                            TagId = detail.TagId,
                            DifficultyId = detail.DifficultyId,
                            Quantity = detail.Quantity
                        })
                        .ToList()
                })
                .ToList()
        };
}

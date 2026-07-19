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

namespace MathInsight.Modules.TestGen.Commands.UpdateBlueprint;

public sealed class UpdateBlueprintCommandHandler
    : IRequestHandler<UpdateBlueprintCommand, Result<UpdateBlueprintResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintAggregateValidator _validator;

    public UpdateBlueprintCommandHandler(
        TestGenDbContext context,
        IBlueprintAggregateValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<UpdateBlueprintResponse>> Handle(
        UpdateBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.RequestInvalid);

        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<UpdateBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        ValidatedBlueprintAggregate? expected = null;
        return await BlueprintExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(
                command,
                validated => expected = validated,
                cancellationToken),
            () => VerifySucceededAsync(command, expected, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<UpdateBlueprintResponse>> ExecuteAsync(
        UpdateBlueprintCommand command,
        Action<ValidatedBlueprintAggregate> captureExpected,
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
            return Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.NotFound);

        if (!string.Equals(blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.MutationForbidden);

        if (blueprint.Status is not (BlueprintStatuses.Draft or BlueprintStatuses.Rejected))
            return Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.StatusInvalid);

        var validationResult = await _validator.ValidateAsync(command.Request, cancellationToken);
        if (validationResult.IsFailure)
            return Result<UpdateBlueprintResponse>.Failure(validationResult.Error!);
        captureExpected(validationResult.Value!);

        var oldDetails = blueprint.Sections.SelectMany(section => section.Details).ToList();
        var oldSections = blueprint.Sections.ToList();
        _context.BlueprintDetails.RemoveRange(oldDetails);
        _context.BlueprintSections.RemoveRange(oldSections);
        await _context.SaveChangesAsync(cancellationToken);

        blueprint.Sections.Clear();
        BlueprintAggregateFactory.Apply(blueprint, validationResult.Value!);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<UpdateBlueprintResponse>.Success(
            new UpdateBlueprintResponse(blueprint.BlueprintId, blueprint.Status));
    }

    private async Task<(bool IsSuccessful, Result<UpdateBlueprintResponse> Result)> VerifySucceededAsync(
        UpdateBlueprintCommand command,
        ValidatedBlueprintAggregate? expected,
        CancellationToken cancellationToken)
    {
        if (expected is null)
            return (false, default!);

        var persisted = await _context.Blueprints
            .AsNoTracking()
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(
                item => item.BlueprintId == command.BlueprintId,
                cancellationToken);

        var succeeded = persisted is not null &&
            string.Equals(persisted.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase) &&
            Matches(persisted, expected);
        return succeeded
            ? (true, Result<UpdateBlueprintResponse>.Success(
                new UpdateBlueprintResponse(persisted!.BlueprintId, persisted.Status)))
            : (false, default!);
    }

    private static bool Matches(
        Blueprint persisted,
        ValidatedBlueprintAggregate expected)
    {
        if (persisted.BlueprintName != expected.BlueprintName ||
            persisted.Grade != expected.Grade ||
            persisted.TotalQuestions != expected.TotalQuestions ||
            persisted.DurationMinutes != expected.DurationMinutes ||
            persisted.Sections.Count != expected.Sections.Count)
        {
            return false;
        }

        var persistedSections = persisted.Sections
            .OrderBy(section => section.SectionOrder)
            .ToList();
        var expectedSections = expected.Sections
            .OrderBy(section => section.SectionOrder)
            .ToList();

        for (var index = 0; index < expectedSections.Count; index++)
        {
            var actualSection = persistedSections[index];
            var expectedSection = expectedSections[index];
            if (actualSection.SectionOrder != expectedSection.SectionOrder ||
                actualSection.SectionCode != expectedSection.SectionCode ||
                actualSection.SectionName != expectedSection.SectionName ||
                actualSection.QuestionType != expectedSection.QuestionType ||
                actualSection.InstructionText != expectedSection.InstructionText ||
                actualSection.TotalQuestions != expectedSection.TotalQuestions ||
                actualSection.DefaultPointPerQuestion != expectedSection.DefaultPointPerQuestion ||
                actualSection.DefaultPointPerPart != expectedSection.DefaultPointPerPart ||
                actualSection.PartCountPerQuestion != expectedSection.PartCountPerQuestion ||
                actualSection.Details.Count != expectedSection.Details.Count)
            {
                return false;
            }

            var actualDetails = actualSection.Details
                .OrderBy(detail => detail.TagId)
                .ThenBy(detail => detail.DifficultyId)
                .ToList();
            var expectedDetails = expectedSection.Details
                .OrderBy(detail => detail.TagId)
                .ThenBy(detail => detail.DifficultyId)
                .ToList();

            for (var detailIndex = 0; detailIndex < expectedDetails.Count; detailIndex++)
            {
                if (actualDetails[detailIndex].TagId != expectedDetails[detailIndex].TagId ||
                    actualDetails[detailIndex].DifficultyId != expectedDetails[detailIndex].DifficultyId ||
                    actualDetails[detailIndex].Quantity != expectedDetails[detailIndex].Quantity)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

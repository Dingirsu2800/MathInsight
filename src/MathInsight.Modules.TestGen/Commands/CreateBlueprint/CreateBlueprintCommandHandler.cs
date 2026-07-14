using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Commands.CreateBlueprint;

public sealed class CreateBlueprintCommandHandler
    : IRequestHandler<CreateBlueprintCommand, Result<CreateBlueprintResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintAggregateValidator _validator;

    public CreateBlueprintCommandHandler(
        TestGenDbContext context,
        IBlueprintAggregateValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<CreateBlueprintResponse>> Handle(
        CreateBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertId) ||
            !await _context.Experts.AsNoTracking().AnyAsync(
                expert => expert.ExpertId == command.ExpertId,
                cancellationToken))
        {
            return Result<CreateBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        }

        var validationResult = await _validator.ValidateAsync(command.Request, cancellationToken);
        if (validationResult.IsFailure)
            return Result<CreateBlueprintResponse>.Failure(validationResult.Error!);

        var blueprint = BlueprintAggregateFactory.Create(validationResult.Value!, command.ExpertId);

        _context.Blueprints.Add(blueprint);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateBlueprintResponse>.Success(
            new CreateBlueprintResponse(blueprint.BlueprintId, blueprint.Status));
    }
}

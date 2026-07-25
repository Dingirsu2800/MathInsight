using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.DeleteBlueprint;

public sealed record DeleteBlueprintCommand(
    string BlueprintId,
    string ExpertId) : IRequest<Result<DeleteBlueprintResponse>>;

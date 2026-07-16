using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.UpdateBlueprint;

public sealed record UpdateBlueprintCommand(
    string BlueprintId,
    BlueprintRequest Request,
    string ExpertId) : IRequest<Result<UpdateBlueprintResponse>>;

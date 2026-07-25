using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.CreateBlueprint;

public sealed record CreateBlueprintCommand(
    BlueprintRequest Request,
    string ExpertId) : IRequest<Result<CreateBlueprintResponse>>;

using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.CloneBlueprint;

public sealed record CloneBlueprintCommand(
    string BlueprintId,
    string ExpertId) : IRequest<Result<CloneBlueprintResponse>>;

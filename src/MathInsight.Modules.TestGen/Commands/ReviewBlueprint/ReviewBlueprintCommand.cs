using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.ReviewBlueprint;

public sealed record ReviewBlueprintCommand(
    string BlueprintId,
    ReviewBlueprintRequest Request,
    string ReviewerExpertId) : IRequest<Result<ReviewBlueprintResponse>>;

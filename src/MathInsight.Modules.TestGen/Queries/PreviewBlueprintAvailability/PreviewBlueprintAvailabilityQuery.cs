using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.PreviewBlueprintAvailability;

public sealed record PreviewBlueprintAvailabilityQuery(BlueprintAvailabilityRequest Request)
    : IRequest<Result<BlueprintAvailabilityResponse>>;

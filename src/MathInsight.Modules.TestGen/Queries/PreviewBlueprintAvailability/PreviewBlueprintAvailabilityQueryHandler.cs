using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.PreviewBlueprintAvailability;

public sealed class PreviewBlueprintAvailabilityQueryHandler
    : IRequestHandler<PreviewBlueprintAvailabilityQuery, Result<BlueprintAvailabilityResponse>>
{
    private readonly IBlueprintAvailabilityChecker _checker;

    public PreviewBlueprintAvailabilityQueryHandler(IBlueprintAvailabilityChecker checker)
    {
        _checker = checker;
    }

    public Task<Result<BlueprintAvailabilityResponse>> Handle(
        PreviewBlueprintAvailabilityQuery request,
        CancellationToken cancellationToken)
        => _checker.PreviewAsync(request.Request, cancellationToken);
}

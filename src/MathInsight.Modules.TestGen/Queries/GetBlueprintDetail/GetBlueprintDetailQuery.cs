using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintDetail;

public sealed record GetBlueprintDetailQuery(string BlueprintId)
    : IRequest<Result<BlueprintDetailResponse>>;

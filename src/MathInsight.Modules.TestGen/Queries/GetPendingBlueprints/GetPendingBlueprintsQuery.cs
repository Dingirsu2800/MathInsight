using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Common;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetPendingBlueprints;

public sealed record GetPendingBlueprintsQuery(
    int PageIndex,
    int PageSize,
    string CurrentExpertId) : IRequest<Result<PagedResponse<BlueprintListItemResponse>>>;

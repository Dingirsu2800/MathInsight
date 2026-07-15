using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Common;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintList;

public sealed record GetBlueprintListQuery(
    int PageIndex,
    int PageSize,
    string? Status,
    int? Grade,
    string? ExpertId,
    string? Search,
    bool IncludeDeactivated,
    string CurrentExpertId) : IRequest<Result<PagedResponse<BlueprintListItemResponse>>>;

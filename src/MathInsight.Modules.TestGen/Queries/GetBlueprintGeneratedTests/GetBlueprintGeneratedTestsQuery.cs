using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintGeneratedTests;

public sealed record GetBlueprintGeneratedTestsQuery(
    string BlueprintId,
    string ExpertId,
    int PageIndex,
    int PageSize) : IRequest<Result<PagedExpertGeneratedTestResponse>>;

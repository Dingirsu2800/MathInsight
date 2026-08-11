using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetFixedTestCandidates;

public sealed record GetFixedTestCandidatesQuery(
    string BlueprintId,
    string ExpertId,
    string BlueprintDetailId,
    string? Search,
    int PageIndex,
    int PageSize) : IRequest<Result<PagedFixedTestCandidateResponse>>;

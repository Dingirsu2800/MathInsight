using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;

public sealed record GetBlueprintExamOptionsQuery(
    string StudentId,
    string? Search = null,
    int PageIndex = 1,
    int PageSize = 20)
    : IRequest<Result<BlueprintExamOptionsResponse>>;

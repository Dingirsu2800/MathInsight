using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetSharedBlueprintExams;

public sealed record GetSharedBlueprintExamsQuery(
    string StudentId,
    int PageIndex,
    int PageSize,
    string? GenerationType = null) : IRequest<Result<PagedSharedBlueprintExamResponse>>;

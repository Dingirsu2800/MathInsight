using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;

public sealed record GetBlueprintExamOptionsQuery(string StudentId)
    : IRequest<Result<IReadOnlyList<BlueprintExamOptionResponse>>>;

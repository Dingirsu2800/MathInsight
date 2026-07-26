using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.ResolveSharedTestCode;

public sealed record ResolveSharedTestCodeQuery(
    string StudentId,
    string TestCode) : IRequest<Result<SharedBlueprintExamResponse>>;

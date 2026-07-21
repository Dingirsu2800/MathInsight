using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Queries.GetDetailedSolution;

public sealed record GetDetailedSolutionQuery(
    string SessionId,
    string StudentId) : IRequest<Result<DetailedSolutionResponse>>;

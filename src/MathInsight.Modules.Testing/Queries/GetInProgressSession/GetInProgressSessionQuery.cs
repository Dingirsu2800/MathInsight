using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Queries.GetInProgressSession;

public sealed record GetInProgressSessionQuery(string TestId, string StudentId)
    : IRequest<Result<string>>;

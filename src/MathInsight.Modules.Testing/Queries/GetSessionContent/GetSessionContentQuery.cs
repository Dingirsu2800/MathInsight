using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Queries.GetSessionContent;

public sealed record GetSessionContentQuery(string SessionId, string StudentId)
    : IRequest<Result<TestSessionViewResponse>>;

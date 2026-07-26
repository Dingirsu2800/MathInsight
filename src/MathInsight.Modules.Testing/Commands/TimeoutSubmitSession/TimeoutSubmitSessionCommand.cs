using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;

public sealed record TimeoutSubmitSessionCommand(string SessionId, string StudentId)
    : IRequest<Result<SubmitSessionResponse>>;

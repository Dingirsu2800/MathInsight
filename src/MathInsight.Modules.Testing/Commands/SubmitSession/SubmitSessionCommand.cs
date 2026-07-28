using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.SubmitSession;

public sealed record SubmitSessionCommand(
    string SessionId,
    string StudentId) : IRequest<Result<SubmitSessionResponse>>;

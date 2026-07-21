using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.StartSession;

public sealed record StartSessionCommand(
    string TestId,
    string StudentId) : IRequest<Result<StartSessionResponse>>;

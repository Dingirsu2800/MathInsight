using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ConfirmEmail;

// UC-93. The ONLY place a self-registered Account row is created.
public record ConfirmEmailCommand(string Token) : IRequest<Result<Unit>>;

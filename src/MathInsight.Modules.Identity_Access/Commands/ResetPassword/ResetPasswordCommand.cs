using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ResetPassword;

// UC-06 part 1. Always succeeds from the caller's perspective (enumeration protection);
// whether an email was actually sent depends on whether the account exists.
public record ResetPasswordCommand(string Email) : IRequest<Result<Unit>>;

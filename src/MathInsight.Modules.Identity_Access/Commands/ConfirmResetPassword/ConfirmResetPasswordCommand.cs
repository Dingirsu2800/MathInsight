using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ConfirmResetPassword;

// UC-06 part 2. Sets a new password given a valid reset token, then revokes every session.
public record ConfirmResetPasswordCommand(string Token, string NewPassword) : IRequest<Result<Unit>>;

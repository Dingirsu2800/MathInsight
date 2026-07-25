using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ChangePassword;

/// <summary>
/// UC-03. Changes the caller's own password. <paramref name="AccountId"/> comes from the
/// authenticated principal's claims, never from the request body.
///
/// Returns a <see cref="LoginResponse"/>: the change revokes every session (BR-15), so the caller
/// is handed a freshly issued token pair to continue with — see the handler for why that keeps
/// BR-15's security property intact.
/// </summary>
public sealed record ChangePasswordCommand(
    string AccountId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<LoginResponse>>;

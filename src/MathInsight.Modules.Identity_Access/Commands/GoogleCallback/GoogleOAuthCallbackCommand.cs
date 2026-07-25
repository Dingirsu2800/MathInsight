using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.GoogleCallback;

// UC-07 (Flow A). Given a Google authorization code, resolves/creates the account and issues the
// same access + refresh token pair as a normal login. State (CSRF) is verified in the controller
// before this runs.
public record GoogleOAuthCallbackCommand(string Code) : IRequest<Result<LoginResponse>>;

using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.RefreshToken;

// UC-95: exchange a valid refresh token for a new access token, rotating the refresh token.
public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;

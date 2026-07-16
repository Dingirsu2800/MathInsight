using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Logout;

/// <summary>
/// UC-02 / BR-10. The refresh token identifies the session to end and is the only required
/// value — logout does not require a valid access token. The jti/exp are best-effort: read from
/// the (possibly expired) bearer token so the access token can be blacklisted for its remaining
/// life when available.
/// </summary>
public record LogoutCommand(
    string RefreshToken,
    string? AccessTokenJti,
    DateTime? AccessTokenExpiresAtUtc) : IRequest;

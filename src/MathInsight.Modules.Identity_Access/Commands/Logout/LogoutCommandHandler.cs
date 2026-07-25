using MathInsight.Modules.Identity_Access.Services.Auth;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAuthSessionService _authSessionService;

    public LogoutCommandHandler(IAuthSessionService authSessionService)
    {
        _authSessionService = authSessionService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // BR-10: the refresh token identifies the session. Resolve the owning account from Redis
        // and delete that refresh token so it cannot be used afterwards. An unknown/expired/
        // already-rotated token yields a null account — logout is then a no-op (idempotent), and
        // the caller still gets a success response, never learning whether the session existed.
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var accountId = await _authSessionService.GetAccountIdByRefreshTokenAsync(request.RefreshToken);

            if (accountId is not null)
            {
                await _authSessionService.RemoveRefreshSessionAsync(accountId, request.RefreshToken);
            }
        }

        // Blacklist the access token's jti for its remaining lifetime, when one could be read from
        // the (possibly expired) bearer token. An already-expired token needs no blacklist entry.
        if (!string.IsNullOrWhiteSpace(request.AccessTokenJti) &&
            request.AccessTokenExpiresAtUtc is DateTime expiresAtUtc)
        {
            var ttl = expiresAtUtc - DateTime.UtcNow;

            if (ttl > TimeSpan.Zero)
            {
                await _authSessionService.BlacklistTokenAsync(request.AccessTokenJti, ttl);
            }
        }
    }
}

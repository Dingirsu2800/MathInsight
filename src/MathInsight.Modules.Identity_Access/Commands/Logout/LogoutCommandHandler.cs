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
        var ttl = request.ExpiresAtUtc - DateTime.UtcNow;

        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await _authSessionService.BlacklistTokenAsync(request.TokenId, ttl);
    }
}
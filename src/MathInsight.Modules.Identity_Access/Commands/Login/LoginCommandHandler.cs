using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuthSessionService _authSessionService;

    public LoginCommandHandler(
        IdentityDbContext dbContext,
        ITokenService tokenService,
        IAuthSessionService authSessionService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _authSessionService = authSessionService;
    }

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var userNameOrEmail = request.UsernameOrEmail.Trim();

        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account =>
            account.Email == userNameOrEmail ||
            account.Username == userNameOrEmail,
            cancellationToken
            );

        if (account is null) return null;
        if (!account.IsActive) return null;

        if (await _authSessionService.IsLockedAsync(account.AccountId))
        {
            return null;
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash);

        if (!isPasswordValid)
        {
            await _authSessionService.RecordFailedLoginAsync(account.AccountId);
            return null;
        }

        await _authSessionService.ResetFailedLoginAsync(account.AccountId);

        var accessToken = _tokenService.CreateAccessToken(account, out DateTime expiresAt);

        if (account.Role.RoleName == "Student")
        {
            await _authSessionService.StoreActiveSessionAsync(
                account.AccountId,
                accessToken,
                expiresAt - DateTime.UtcNow);
        }

        return new LoginResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Username = account.Username,
            RoleName = account.Role.RoleName,
            AccessToken = accessToken,
            ExpiresAt = expiresAt
        };
    }
}


using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ConfirmResetPassword;

/// <summary>
/// UC-06 part 2. Validates the reset token against Redis, hashes and stores the new password
/// (BR-08 policy is enforced at the DTO), consumes the token, and revokes every existing session
/// for the account (BR-15). BR-08 password strength is validated at the API boundary.
/// </summary>
public class ConfirmResetPasswordCommandHandler : IRequestHandler<ConfirmResetPasswordCommand, Result<Unit>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordResetTokenStore _resetTokens;
    private readonly IAuthSessionService _authSessionService;

    public ConfirmResetPasswordCommandHandler(
        IdentityDbContext dbContext,
        IPasswordResetTokenStore resetTokens,
        IAuthSessionService authSessionService)
    {
        _dbContext = dbContext;
        _resetTokens = resetTokens;
        _authSessionService = authSessionService;
    }

    public async Task<Result<Unit>> Handle(ConfirmResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Missing key ⇒ token expired (15m) or already used.
        var accountId = await _resetTokens.GetAccountIdAsync(request.Token, cancellationToken);

        if (accountId is null)
        {
            return Result<Unit>.Failure(AuthErrors.TokenExpired);
        }

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(account => account.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            // The account disappeared after the token was issued — treat the token as spent.
            await _resetTokens.DeleteAsync(request.Token, cancellationToken);
            return Result<Unit>.Failure(AuthErrors.TokenExpired);
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Single-use: consume the token so it cannot be replayed.
        await _resetTokens.DeleteAsync(request.Token, cancellationToken);

        // BR-15: a password reset invalidates every existing access and refresh token.
        await _authSessionService.RevokeAllSessionsAsync(accountId);

        return Result<Unit>.Success(Unit.Value);
    }
}

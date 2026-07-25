using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ChangePassword;

/// <summary>
/// UC-03. Re-verifies the caller's current password, stores the new one as a BCrypt hash
/// (strength 12, per BR-08 / the Assumptions section), revokes every existing session for the
/// account (BR-15), and then issues a fresh session for the caller.
///
/// The revoke-then-reissue order is what makes this safe. RevokeAllSessionsAsync kills every
/// token that existed before the change — so an attacker holding a stolen token on another
/// device is logged out, which is the security property BR-15 exists to guarantee. The new pair
/// is minted afterwards and is therefore unaffected by that sweep: only the caller, who proved
/// knowledge of the current password, gets to keep a working session. This is the standard
/// "change password without logging yourself out" behaviour.
///
/// BR-08 password strength is validated at the API boundary by ChangePasswordRequest.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<LoginResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuthSessionService _authSessionService;

    public ChangePasswordCommandHandler(
        IdentityDbContext dbContext,
        ITokenService tokenService,
        IAuthSessionService authSessionService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _authSessionService = authSessionService;
    }

    public async Task<Result<LoginResponse>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Role is required: CreateAccessToken writes Role.RoleName into the token's claims.
        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.AccountId == request.AccountId, cancellationToken);

        // The token authenticated an account that no longer exists — treat the token as invalid.
        if (account is null)
        {
            return Result<LoginResponse>.Failure(AuthErrors.TokenInvalid);
        }

        // No usable local password (see NoPasswordSet). Checked before Verify, which throws
        // SaltParseException rather than returning false on a malformed hash.
        if (!IsUsableBCryptHash(account.PasswordHash))
        {
            return Result<LoginResponse>.Failure(AuthErrors.NoPasswordSet);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash))
        {
            return Result<LoginResponse>.Failure(AuthErrors.InvalidCurrentPassword);
        }

        // Compared as plaintext: BCrypt salts every hash, so re-hashing the new password would
        // never equal the stored hash even when the passwords are identical.
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return Result<LoginResponse>.Failure(AuthErrors.SamePassword);
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // BR-15: invalidate every outstanding access and refresh token, including the caller's
        // own — every session that existed before this moment is now dead.
        await _authSessionService.RevokeAllSessionsAsync(account.AccountId);

        // Then mint a replacement session for the caller only, exactly as login does.
        var accessToken = _tokenService.CreateAccessToken(account, out var expiresAt, out var tokenId);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(
            account.AccountId,
            tokenId,
            expiresAt,
            cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Username = account.Username,
            RoleName = account.Role.RoleName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    // A BCrypt hash always carries its "$2a$"/"$2b$"/"$2y$" prefix. Anything else (empty, or a
    // placeholder written by a non-password account creation path) means there is no password to
    // verify against.
    private static bool IsUsableBCryptHash(string? passwordHash) =>
        !string.IsNullOrWhiteSpace(passwordHash) && passwordHash.StartsWith("$2", StringComparison.Ordinal);
}

using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuthSessionService _authSessionService;

    public RefreshTokenCommandHandler(
        IdentityDbContext dbContext,
        ITokenService tokenService,
        IAuthSessionService authSessionService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _authSessionService = authSessionService;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Missing/expired/revoked/already-rotated → 401 (single-use rotation makes reuse fail).
        var accountId = await _authSessionService.GetAccountIdByRefreshTokenAsync(request.RefreshToken);

        if (accountId is null)
        {
            return Result<LoginResponse>.Failure(AuthErrors.TokenInvalid);
        }

        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            return Result<LoginResponse>.Failure(AuthErrors.TokenInvalid);
        }

        // A deactivated user must not be able to refresh their way back in.
        if (!account.IsActive)
        {
            return Result<LoginResponse>.Failure(AuthErrors.AccountDeactivated);
        }

        var accessToken = _tokenService.CreateAccessToken(account, out var expiresAt, out var tokenId);

        var rotation = await _tokenService.RotateRefreshTokenAsync(
            request.RefreshToken,
            tokenId,
            expiresAt,
            cancellationToken);

        // The token disappeared between validation and rotation (concurrent use) → treat as invalid.
        if (rotation is null)
        {
            return Result<LoginResponse>.Failure(AuthErrors.TokenInvalid);
        }

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Username = account.Username,
            RoleName = account.Role.RoleName,
            AccessToken = accessToken,
            RefreshToken = rotation.RefreshToken,
            ExpiresAt = expiresAt
        });
    }
}

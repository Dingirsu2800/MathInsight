using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
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

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var userNameOrEmail = request.UsernameOrEmail.Trim();

        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account =>
                account.Email == userNameOrEmail ||
                account.Username == userNameOrEmail,
                cancellationToken);

        // Not found → generic 401 (indistinguishable from a wrong password, per BR-03). There is
        // no per-account failure counter to increment because no account exists.
        if (account is null)
        {
            return Result<LoginResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        if (await _authSessionService.IsLockedAsync(account.AccountId))
        {
            return Result<LoginResponse>.Failure(AuthErrors.AccountLocked);
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash);

        if (!isPasswordValid)
        {
            await _authSessionService.RecordFailedLoginAsync(account.AccountId);
            return Result<LoginResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        // Credentials are correct; brute-force protection no longer applies to this attempt.
        await _authSessionService.ResetFailedLoginAsync(account.AccountId);

        // DD-01: is_active = false is unambiguous — it can only mean Admin deactivation.
        if (!account.IsActive)
        {
            return Result<LoginResponse>.Failure(AuthErrors.AccountDeactivated);
        }

        // BR-06: a confirmed Teacher cannot log in until Admin approves the application.
        if (IsRole(account, "Teacher"))
        {
            var application = await _dbContext.TeacherApplications
                .Where(application => application.TeacherId == account.AccountId)
                .OrderByDescending(application => application.AppliedTime)
                .FirstOrDefaultAsync(cancellationToken);

            // DB stores title-case status values: 'Pending', 'Approved', 'Rejected'.
            if (IsStatus(application?.Status, "Rejected"))
            {
                return Result<LoginResponse>.Failure(
                    AuthErrors.ApplicationRejected(application!.ReviewComments));
            }

            // Anything that is not explicitly Approved (Pending, or a missing row) blocks login.
            if (!IsStatus(application?.Status, "Approved"))
            {
                return Result<LoginResponse>.Failure(AuthErrors.ApplicationPending);
            }
        }

        // BR-02: a Student may only hold one session — drop any previous refresh token(s) first.
        if (IsRole(account, "Student"))
        {
            await _authSessionService.RevokeAllSessionsAsync(account.AccountId);
        }

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

    private static bool IsRole(Entities.Account account, string roleName) =>
        string.Equals(account.Role.RoleName, roleName, StringComparison.OrdinalIgnoreCase);

    private static bool IsStatus(string? status, string expected) =>
        string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
}

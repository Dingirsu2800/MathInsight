using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Entities;
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

        // BR-06. Login itself no longer depends on approval: a Pending or Rejected Teacher signs in
        // and reaches ONLY the self-service application endpoints. Real teacher features are gated
        // per request by the "TeacherApproved" policy, which re-reads the status from the database
        // — so the token issued here grants nothing an unapproved teacher should not have.
        string? applicationStatus = null;

        if (IsRole(account, "Teacher"))
        {
            // DB stores title-case status values: 'Pending', 'Approved', 'Rejected'.
            var status = await _dbContext.TeacherApplications
                .AsNoTracking()
                .Where(application => application.TeacherId == account.AccountId)
                .OrderByDescending(application => application.AppliedTime)
                .Select(application => application.Status)
                .FirstOrDefaultAsync(cancellationToken);

            applicationStatus = ToLoginApplicationStatus(status);
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
            ExpiresAt = expiresAt,
            ApplicationStatus = applicationStatus
        });
    }

    private static bool IsRole(Entities.Account account, string roleName) =>
        string.Equals(account.Role.RoleName, roleName, StringComparison.OrdinalIgnoreCase);

    private static bool IsStatus(string? status, string expected) =>
        string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps the stored title-case status onto the lowercase wire vocabulary. A missing row means
    /// the Teacher was created by an Admin (UC-11) and never filed an application — "none", which
    /// the client treats as a normal login.
    /// </summary>
    private static string ToLoginApplicationStatus(string? status)
    {
        if (status is null) return LoginApplicationStatus.None;
        if (IsStatus(status, TeacherApplication.StatusApproved)) return LoginApplicationStatus.Approved;
        if (IsStatus(status, TeacherApplication.StatusRejected)) return LoginApplicationStatus.Rejected;
        if (IsStatus(status, TeacherApplication.StatusPending)) return LoginApplicationStatus.Pending;

        // CK_TeacherApplication_Status permits nothing else; treat any surprise as unapproved.
        return LoginApplicationStatus.Pending;
    }
}

using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.GoogleCallback;

/// <summary>
/// UC-07, BR-07, DD-01. Exchanges the Google authorization code for a verified profile, then:
/// links/logs in a matching confirmed account, or creates a new Student account (is_active = true,
/// no confirmation email, no pending-registration record). Issues tokens via the shared
/// <see cref="ITokenService"/> / <see cref="IAuthSessionService"/> — the same path as normal login.
/// </summary>
public class GoogleOAuthCallbackCommandHandler
    : IRequestHandler<GoogleOAuthCallbackCommand, Result<LoginResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly ITokenService _tokenService;
    private readonly IAuthSessionService _authSessionService;
    private readonly IPublisher _publisher;

    public GoogleOAuthCallbackCommandHandler(
        IdentityDbContext dbContext,
        IGoogleOAuthService googleOAuthService,
        ITokenService tokenService,
        IAuthSessionService authSessionService,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _googleOAuthService = googleOAuthService;
        _tokenService = tokenService;
        _authSessionService = authSessionService;
        _publisher = publisher;
    }

    public async Task<Result<LoginResponse>> Handle(GoogleOAuthCallbackCommand request, CancellationToken cancellationToken)
    {
        var profile = await _googleOAuthService.ExchangeCodeForProfileAsync(request.Code, cancellationToken);

        if (profile is null || !profile.EmailVerified)
        {
            // Bad/expired code, network failure, or Google has not verified this email.
            return Result<LoginResponse>.Failure(AuthErrors.GoogleAuthFailed);
        }

        var email = profile.Email.Trim();

        // DD-01: every persisted account is a confirmed account, so this is the confirmed-account
        // lookup. (SQL Server's default collation makes the comparison case-insensitive.)
        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.Email == email, cancellationToken);

        if (account is null)
        {
            account = await CreateStudentAccountAsync(profile, email, cancellationToken);
        }
        else
        {
            // A Google login must not let a deactivated account back in (UC-14).
            if (!account.IsActive)
            {
                return Result<LoginResponse>.Failure(AuthErrors.AccountDeactivated);
            }

            // BR-07: link the Google identity to the existing account instead of creating a
            // duplicate. Only set it once; never overwrite an existing link.
            if (string.IsNullOrEmpty(account.GoogleSubId))
            {
                account.GoogleSubId = profile.Sub;
                account.GoogleEmail = email;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        // BR-02: a Student may hold only one session — drop any previous refresh token(s) first.
        if (string.Equals(account.Role.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            await _authSessionService.RevokeAllSessionsAsync(account.AccountId);
        }

        // Identical token issuance to the normal login path (reused, not duplicated).
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
        });
    }

    private async Task<Account> CreateStudentAccountAsync(
        GoogleUserProfile profile,
        string email,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.RoleName == "Student", cancellationToken)
            ?? throw new InvalidOperationException("Seeded role 'Student' was not found.");

        var accountId = Guid.NewGuid().ToString();
        var username = await GenerateUniqueUsernameAsync(email, cancellationToken);

        var account = new Account
        {
            AccountId = accountId,
            Username = username,
            // Google logins carry no password. Store an unusable random hash so the NOT NULL column
            // is satisfied and password login for this account is impossible (BR-08 hashing).
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 12),
            Email = email,
            FirstName = Truncate(ResolveFirstName(profile.FirstName, email), 50),
            LastName = Truncate(profile.LastName?.Trim() ?? string.Empty, 50),
            RoleId = role.RoleId,
            IsActive = true, // BR-07: Google already verified the email — no confirmation needed.
            CreatedTime = DateTime.UtcNow,
            GoogleSubId = profile.Sub,
            GoogleEmail = email,
        };

        _dbContext.Accounts.Add(account);
        _dbContext.Students.Add(new Student { StudentId = accountId });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Populate the navigation for the token issuance that follows.
        account.Role = role;

        await _publisher.Publish(new AccountCreatedEvent
        {
            AccountId = accountId,
            Email = email,
            Username = username,
            RoleName = role.RoleName,
            FirstName = account.FirstName,
            LastName = account.LastName,
        }, cancellationToken);

        return account;
    }

    // Derives a unique username from the email local part, appending a short suffix on collision.
    private async Task<string> GenerateUniqueUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var localPart = email.Split('@')[0];
        var sanitized = new string(localPart.Where(char.IsLetterOrDigit).ToArray());

        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "user";
        }

        if (sanitized.Length > 40)
        {
            sanitized = sanitized[..40];
        }

        var candidate = sanitized;
        while (await _dbContext.Accounts.AnyAsync(account => account.Username == candidate, cancellationToken))
        {
            candidate = $"{sanitized}_{Guid.NewGuid().ToString("N")[..6]}";
        }

        return candidate;
    }

    private static string ResolveFirstName(string? firstName, string email)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            return firstName.Trim();
        }

        var localPart = email.Split('@')[0];
        return string.IsNullOrWhiteSpace(localPart) ? "Google User" : localPart;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

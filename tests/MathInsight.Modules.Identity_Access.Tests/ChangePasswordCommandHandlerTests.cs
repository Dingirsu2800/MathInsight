using MathInsight.Modules.Identity_Access.Commands.ChangePassword;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for ChangePasswordCommandHandler (UC-03, BR-08, BR-15).
///
/// The shape to protect here is revoke-then-reissue: every pre-existing session dies, and only the
/// caller — who proved knowledge of the current password — walks away with a working token pair.
/// ITokenService and IAuthSessionService are Moq doubles; EF Core InMemory stands in for SQL
/// Server. BCrypt runs for real, so the verification and hashing assertions are genuine.
/// </summary>
public class ChangePasswordCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";

    private const string AccountId = "acct-A";
    private const string Email = "user-a@mathinsight.test";
    private const string Username = "user_a";
    private const string CurrentPassword = "Current#Password1";
    private const string NewPassword = "Brand#New1Password";

    private const string AccessToken = "access-token-value";
    private const string RefreshToken = "refresh-token-value";
    private const string TokenId = "jti-0001";
    private static readonly DateTime ExpiresAt = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    // BCrypt is deliberately slow; hash the current password once for the whole class.
    private static readonly string CurrentPasswordHash = BCrypt.Net.BCrypt.HashPassword(CurrentPassword);

    private readonly IdentityDbContext _db;
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IAuthSessionService> _sessionService = new();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        EnsureRole(StudentRoleId, "Student");

        var expiresAt = ExpiresAt;
        var tokenId = TokenId;
        _tokenService
            .Setup(t => t.CreateAccessToken(It.IsAny<Account>(), out expiresAt, out tokenId))
            .Returns(AccessToken);
        _tokenService
            .Setup(t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshToken);

        _handler = new ChangePasswordCommandHandler(_db, _tokenService.Object, _sessionService.Object);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Roles may already be present via RoleConfiguration.HasData; only add what is missing.</summary>
    private void EnsureRole(string roleId, string roleName)
    {
        if (_db.Roles.Any(role => role.RoleId == roleId))
        {
            return;
        }

        _db.Roles.Add(new Role { RoleId = roleId, RoleName = roleName });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void SeedAccount(string? passwordHash = null, bool isActive = true)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = AccountId,
            Username = Username,
            Email = Email,
            PasswordHash = passwordHash ?? CurrentPasswordHash,
            FirstName = "Test",
            LastName = "User",
            RoleId = StudentRoleId,
            IsActive = isActive,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Task<MathInsight.Shared.Results.Result<MathInsight.Modules.Identity_Access.Contracts.Auth.LoginResponse>>
        ChangeAsync(
            string currentPassword = CurrentPassword,
            string newPassword = NewPassword,
            string accountId = AccountId,
            CancellationToken cancellationToken = default) =>
        _handler.Handle(
            new ChangePasswordCommand(accountId, currentPassword, newPassword), cancellationToken);

    private Task<Account> LoadAsync() =>
        _db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == AccountId);

    private void VerifyNoSessionChurn()
    {
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
        _tokenService.Verify(
            t => t.CreateAccessToken(It.IsAny<Account>(), out It.Ref<DateTime>.IsAny, out It.Ref<string>.IsAny),
            Times.Never);
        _tokenService.Verify(
            t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ValidChange_Succeeds()
    {
        SeedAccount();

        var result = await ChangeAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidChange_PersistsTheNewPasswordAndRetiresTheOldOne()
    {
        SeedAccount();

        await ChangeAsync();

        var account = await LoadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(CurrentPassword, account.PasswordHash));
    }

    [Fact]
    public async Task ValidChange_StoresABCryptHashNeverThePlaintext()
    {
        // BR-08: the raw password must never reach the column.
        SeedAccount();

        await ChangeAsync();

        var account = await LoadAsync();
        Assert.NotEqual(NewPassword, account.PasswordHash);
        Assert.StartsWith("$2", account.PasswordHash);
        Assert.NotEqual(CurrentPasswordHash, account.PasswordHash);
    }

    [Fact]
    public async Task ValidChange_ReturnsAFreshSessionAndTheCallersProfile()
    {
        SeedAccount();

        var result = await ChangeAsync();

        var dto = result.Value!;
        Assert.Equal(AccountId, dto.AccountId);
        Assert.Equal(Email, dto.Email);
        Assert.Equal(Username, dto.Username);
        Assert.Equal("Student", dto.RoleName);
        Assert.Equal(AccessToken, dto.AccessToken);
        Assert.Equal(RefreshToken, dto.RefreshToken);
        Assert.Equal(ExpiresAt, dto.ExpiresAt);
        Assert.Null(dto.ApplicationStatus);   // not a teacher-application response
    }

    [Fact]
    public async Task ValidChange_RevokesEveryExistingSession()
    {
        // BR-15: a stolen token on another device is dead the moment the password changes.
        SeedAccount();

        await ChangeAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task ValidChange_RevokesBeforeIssuingTheReplacementSession()
    {
        // The whole safety of the design rests on this order: reissuing first would have the sweep
        // kill the caller's brand-new tokens too.
        SeedAccount();
        var calls = new List<string>();
        _sessionService
            .Setup(s => s.RevokeAllSessionsAsync(It.IsAny<string>()))
            .Callback(() => calls.Add("revoke"))
            .Returns(Task.CompletedTask);
        _tokenService
            .Setup(t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("issue"))
            .ReturnsAsync(RefreshToken);

        await ChangeAsync();

        Assert.Equal(new[] { "revoke", "issue" }, calls);
    }

    [Fact]
    public async Task ValidChange_PersistsThePasswordBeforeRevoking()
    {
        // Revoking first would log everyone out of a change that might still fail to save.
        SeedAccount();
        var passwordChangedAtRevokeTime = false;
        _sessionService
            .Setup(s => s.RevokeAllSessionsAsync(It.IsAny<string>()))
            .Callback(() => passwordChangedAtRevokeTime =
                _db.Accounts.AsNoTracking().Single(a => a.AccountId == AccountId).PasswordHash != CurrentPasswordHash)
            .Returns(Task.CompletedTask);

        await ChangeAsync();

        Assert.True(passwordChangedAtRevokeTime);
    }

    [Fact]
    public async Task ValidChange_BindsRefreshTokenToTheNewAccessTokenJtiAndExpiry()
    {
        // DD-02: the pair must be minted together or revocation cannot blacklist the right token.
        SeedAccount();

        await ChangeAsync();

        _tokenService.Verify(
            t => t.IssueRefreshTokenAsync(AccountId, TokenId, ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidChange_LeavesOtherAccountFieldsUntouched()
    {
        SeedAccount();

        await ChangeAsync();

        var account = await LoadAsync();
        Assert.Equal(Email, account.Email);
        Assert.Equal(Username, account.Username);
        Assert.Equal(StudentRoleId, account.RoleId);
        Assert.True(account.IsActive);
    }

    // ---------------------------------------------------------------- account resolution

    [Fact]
    public async Task AccountNotFound_ReturnsTokenInvalid()
    {
        // The access token authenticated an account that no longer exists.
        var result = await ChangeAsync(accountId: "deleted-account");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenInvalid.Code, result.Error!.Code);
        VerifyNoSessionChurn();
    }

    [Fact]
    public async Task DeactivatedAccount_PasswordIsStillChanged()
    {
        // Documents current behaviour: this handler does NOT check IsActive, so a deactivated
        // account holding a still-valid access token can change its own password.
        SeedAccount(isActive: false);

        var result = await ChangeAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash));
        Assert.False(account.IsActive);   // deactivation itself is unchanged
    }

    // ---------------------------------------------------------------- no usable password

    [Fact]
    public async Task AccountWithEmptyHash_ReturnsNoPasswordSet()
    {
        // Guarded before Verify, which throws SaltParseException rather than returning false.
        SeedAccount(passwordHash: string.Empty);

        var result = await ChangeAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.NoPasswordSet.Code, result.Error!.Code);
        VerifyNoSessionChurn();
    }

    [Fact]
    public async Task AccountWithNonBCryptHash_ReturnsNoPasswordSet()
    {
        // A placeholder written by a non-password creation path is not a verifiable credential.
        SeedAccount(passwordHash: "google-oauth-no-local-password");

        var result = await ChangeAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.NoPasswordSet.Code, result.Error!.Code);
        var account = await LoadAsync();
        Assert.Equal("google-oauth-no-local-password", account.PasswordHash);   // untouched
    }

    [Fact]
    public async Task AccountWithWhitespaceHash_ReturnsNoPasswordSet()
    {
        SeedAccount(passwordHash: "   ");

        var result = await ChangeAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.NoPasswordSet.Code, result.Error!.Code);
    }

    // ---------------------------------------------------------------- current-password check

    [Fact]
    public async Task WrongCurrentPassword_ReturnsInvalidCurrentPassword()
    {
        SeedAccount();

        var result = await ChangeAsync(currentPassword: "Not#TheCurrent1");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCurrentPassword.Code, result.Error!.Code);
    }

    [Fact]
    public async Task WrongCurrentPassword_WritesNothingAndTouchesNoSession()
    {
        SeedAccount();

        await ChangeAsync(currentPassword: "Not#TheCurrent1");

        var account = await LoadAsync();
        Assert.Equal(CurrentPasswordHash, account.PasswordHash);   // stored hash unchanged
        VerifyNoSessionChurn();
    }

    [Fact]
    public async Task CurrentPasswordIsVerifiedAgainstTheHash_NotComparedAsText()
    {
        // Submitting the stored hash itself must fail: a naive string comparison against the
        // PasswordHash column would let anyone who read the database change the password.
        SeedAccount();

        var result = await ChangeAsync(currentPassword: CurrentPasswordHash);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCurrentPassword.Code, result.Error!.Code);
    }

    [Fact]
    public async Task EmptyCurrentPassword_IsRejected()
    {
        SeedAccount();

        var result = await ChangeAsync(currentPassword: string.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCurrentPassword.Code, result.Error!.Code);
    }

    // ---------------------------------------------------------------- reuse rule

    [Fact]
    public async Task NewPasswordIdenticalToCurrent_IsRejected()
    {
        // Compared as plaintext on purpose: BCrypt salts every hash, so re-hashing would never
        // match the stored value even for the same password.
        SeedAccount();

        var result = await ChangeAsync(newPassword: CurrentPassword);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.SamePassword.Code, result.Error!.Code);
        var account = await LoadAsync();
        Assert.Equal(CurrentPasswordHash, account.PasswordHash);   // nothing rewritten
        VerifyNoSessionChurn();
    }

    [Fact]
    public async Task NewPasswordDifferingOnlyByCase_IsAccepted()
    {
        // The reuse check is an ordinal comparison, so a case variant counts as a different
        // password and goes through.
        SeedAccount();
        var caseVariant = CurrentPassword.ToUpperInvariant();

        var result = await ChangeAsync(newPassword: caseVariant);

        Assert.True(result.IsSuccess);
        var account = await LoadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(caseVariant, account.PasswordHash));
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ChangeAsync(cancellationToken: cts.Token));
    }
}

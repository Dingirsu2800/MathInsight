using MathInsight.Modules.Identity_Access.Commands.GoogleCallback;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for GoogleOAuthCallbackCommandHandler (UC-07, BR-02, BR-07, DD-01).
///
/// The code-for-profile exchange is entirely behind IGoogleOAuthService, so no network call is made
/// here — the double returns a GoogleUserProfile (or null to model a bad code). ITokenService,
/// IAuthSessionService and IPublisher are Moq doubles; EF Core InMemory stands in for SQL Server.
///
/// Note on scope: the CSRF `state` is verified and consumed by the controller before this handler
/// runs (IOAuthStateStore), so it is deliberately not exercised here.
/// </summary>
public class GoogleOAuthCallbackCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";
    private const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";

    private const string Code = "google-auth-code";
    private const string GoogleSub = "google-sub-1234567890";
    private const string Email = "person@gmail.test";

    private const string AccessToken = "access-token-value";
    private const string RefreshToken = "refresh-token-value";
    private const string TokenId = "jti-0001";
    private static readonly DateTime ExpiresAt = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly IdentityDbContext _db;
    private readonly Mock<IGoogleOAuthService> _googleOAuth = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IAuthSessionService> _sessionService = new();
    private readonly Mock<IPublisher> _publisher = new();
    private readonly GoogleOAuthCallbackCommandHandler _handler;

    public GoogleOAuthCallbackCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        EnsureRole(StudentRoleId, "Student");
        EnsureRole(TeacherRoleId, "Teacher");

        var expiresAt = ExpiresAt;
        var tokenId = TokenId;
        _tokenService
            .Setup(t => t.CreateAccessToken(It.IsAny<Account>(), out expiresAt, out tokenId))
            .Returns(AccessToken);
        _tokenService
            .Setup(t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshToken);

        _handler = new GoogleOAuthCallbackCommandHandler(
            _db, _googleOAuth.Object, _tokenService.Object, _sessionService.Object, _publisher.Object);
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

    private void GivenGoogleProfile(
        string email = Email,
        bool emailVerified = true,
        string sub = GoogleSub,
        string? firstName = "Ada",
        string? lastName = "Lovelace") =>
        _googleOAuth
            .Setup(g => g.ExchangeCodeForProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleUserProfile(sub, email, emailVerified, firstName, lastName));

    private void GivenGoogleExchangeFails() =>
        _googleOAuth
            .Setup(g => g.ExchangeCodeForProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleUserProfile?)null);

    private Account SeedAccount(
        string email = Email,
        string username = "existing_user",
        string accountId = "acct-existing",
        string roleId = StudentRoleId,
        bool isActive = true,
        string? googleSubId = null,
        string? googleEmail = null)
    {
        var account = new Account
        {
            AccountId = accountId,
            Username = username,
            Email = email,
            PasswordHash = "$2a$12$abcdefghijklmnopqrstuvwxyz012345678901234567890123456",
            FirstName = "Existing",
            LastName = "User",
            RoleId = roleId,
            IsActive = isActive,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            GoogleSubId = googleSubId,
            GoogleEmail = googleEmail
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return account;
    }

    private Task<MathInsight.Shared.Results.Result<MathInsight.Modules.Identity_Access.Contracts.Auth.LoginResponse>>
        CallbackAsync(CancellationToken cancellationToken = default) =>
        _handler.Handle(new GoogleOAuthCallbackCommand(Code), cancellationToken);

    private Task<Account> LoadByEmailAsync(string email = Email) =>
        _db.Accounts.AsNoTracking().SingleAsync(a => a.Email == email);

    private void VerifyNoTokensIssued()
    {
        _tokenService.Verify(
            t => t.CreateAccessToken(It.IsAny<Account>(), out It.Ref<DateTime>.IsAny, out It.Ref<string>.IsAny),
            Times.Never);
        _tokenService.Verify(
            t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------- failed exchange

    [Fact]
    public async Task ExchangeFails_ReturnsGoogleAuthFailed()
    {
        // Bad or expired code, or a network failure — the service fails closed with null.
        GivenGoogleExchangeFails();

        var result = await CallbackAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.GoogleAuthFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ExchangeFails_CreatesNoAccountAndIssuesNoTokens()
    {
        GivenGoogleExchangeFails();

        await CallbackAsync();

        Assert.False(await _db.Accounts.AnyAsync(a => a.Email == Email));
        VerifyNoTokensIssued();
        _publisher.Verify(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_ReturnsGoogleAuthFailed()
    {
        // BR-07 rests on Google having verified the address; an unverified one would let a caller
        // claim an email they do not own.
        GivenGoogleProfile(emailVerified: false);

        var result = await CallbackAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.GoogleAuthFailed.Code, result.Error!.Code);
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_CreatesNoAccountAndCannotHijackAnExistingOne()
    {
        SeedAccount(email: Email, googleSubId: null);
        GivenGoogleProfile(emailVerified: false);

        await CallbackAsync();

        var account = await LoadByEmailAsync();
        Assert.Null(account.GoogleSubId);   // no link made
        Assert.Equal(1, await _db.Accounts.CountAsync(a => a.Email == Email));
        VerifyNoTokensIssued();
    }

    // ---------------------------------------------------------------- first-time login (auto-create)

    [Fact]
    public async Task UnknownEmail_CreatesAnActiveStudentAccount()
    {
        // BR-07/DD-01: Google already verified the email, so the account is created active with no
        // confirmation email and no pending-registration record.
        GivenGoogleProfile();

        var result = await CallbackAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadByEmailAsync();
        Assert.True(account.IsActive);
        Assert.Equal(StudentRoleId, account.RoleId);
        Assert.Equal(GoogleSub, account.GoogleSubId);
        Assert.Equal(Email, account.GoogleEmail);
        Assert.True(Guid.TryParse(account.AccountId, out _));
    }

    [Fact]
    public async Task UnknownEmail_CreatesTheLinkedStudentRow()
    {
        GivenGoogleProfile();

        await CallbackAsync();

        var account = await LoadByEmailAsync();
        var student = await _db.Students.AsNoTracking().SingleAsync();
        Assert.Equal(account.AccountId, student.StudentId);
    }

    [Fact]
    public async Task UnknownEmail_StoresAnUnusableBCryptHashSoPasswordLoginIsImpossible()
    {
        // The column is NOT NULL, so a hash must exist — but it is a random value nobody knows.
        GivenGoogleProfile();

        await CallbackAsync();

        var account = await LoadByEmailAsync();
        Assert.StartsWith("$2", account.PasswordHash);
        Assert.False(BCrypt.Net.BCrypt.Verify(string.Empty, account.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(GoogleSub, account.PasswordHash));
    }

    [Fact]
    public async Task UnknownEmail_DerivesTheUsernameFromTheEmailLocalPart()
    {
        GivenGoogleProfile(email: "ada.lovelace@gmail.test");

        await CallbackAsync();

        var account = await LoadByEmailAsync("ada.lovelace@gmail.test");
        Assert.Equal("adalovelace", account.Username);   // non-alphanumerics stripped
    }

    [Fact]
    public async Task UnknownEmail_SuffixesTheUsernameOnCollision()
    {
        // A second Google user whose local part matches an existing username must not collide.
        SeedAccount(email: "someone.else@mathinsight.test", username: "adalovelace", accountId: "acct-existing");
        GivenGoogleProfile(email: "ada.lovelace@gmail.test");

        await CallbackAsync();

        var account = await LoadByEmailAsync("ada.lovelace@gmail.test");
        Assert.NotEqual("adalovelace", account.Username);
        Assert.StartsWith("adalovelace_", account.Username);
    }

    [Fact]
    public async Task UnknownEmail_FallsBackToTheLocalPartWhenGoogleSendsNoFirstName()
    {
        GivenGoogleProfile(email: "ada@gmail.test", firstName: null, lastName: null);

        await CallbackAsync();

        var account = await LoadByEmailAsync("ada@gmail.test");
        Assert.Equal("ada", account.FirstName);
        Assert.Equal(string.Empty, account.LastName);
    }

    [Fact]
    public async Task UnknownEmail_TruncatesOverlongNamesToTheColumnWidth()
    {
        GivenGoogleProfile(firstName: new string('A', 80), lastName: new string('B', 80));

        await CallbackAsync();

        var account = await LoadByEmailAsync();
        Assert.Equal(50, account.FirstName.Length);
        Assert.Equal(50, account.LastName.Length);
    }

    [Fact]
    public async Task UnknownEmail_PublishesAccountCreatedEvent()
    {
        AccountCreatedEvent? published = null;
        _publisher
            .Setup(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AccountCreatedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);
        GivenGoogleProfile();

        await CallbackAsync();

        Assert.NotNull(published);
        Assert.Equal(Email, published!.Email);
        Assert.Equal("Student", published.RoleName);
    }

    [Fact]
    public async Task UnknownEmail_PublishesOnlyAfterTheAccountIsDurable()
    {
        var accountVisibleAtPublishTime = false;
        _publisher
            .Setup(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => accountVisibleAtPublishTime = _db.Accounts.Any(a => a.Email == Email))
            .Returns(Task.CompletedTask);
        GivenGoogleProfile();

        await CallbackAsync();

        Assert.True(accountVisibleAtPublishTime);
    }

    // ---------------------------------------------------------------- returning Google user

    [Fact]
    public async Task AlreadyLinkedAccount_LogsInWithoutCreatingADuplicate()
    {
        SeedAccount(googleSubId: GoogleSub, googleEmail: Email);
        GivenGoogleProfile();

        var result = await CallbackAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _db.Accounts.CountAsync(a => a.Email == Email));
        Assert.Equal("acct-existing", result.Value!.AccountId);
        _publisher.Verify(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlreadyLinkedAccount_DoesNotOverwriteTheExistingLink()
    {
        // "Only set it once; never overwrite" — a different sub arriving for the same email must
        // not silently repoint the account at another Google identity.
        SeedAccount(googleSubId: "original-sub", googleEmail: Email);
        GivenGoogleProfile(sub: "different-sub");

        await CallbackAsync();

        var account = await LoadByEmailAsync();
        Assert.Equal("original-sub", account.GoogleSubId);
    }

    // ---------------------------------------------------------------- BR-07 account linking

    [Fact]
    public async Task PasswordAccountWithMatchingEmail_IsLinkedNotRejected()
    {
        // BR-07: a normally-registered account is linked to the Google identity rather than
        // treated as a duplicate-email conflict.
        SeedAccount(googleSubId: null, googleEmail: null);
        GivenGoogleProfile();

        var result = await CallbackAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadByEmailAsync();
        Assert.Equal(GoogleSub, account.GoogleSubId);
        Assert.Equal(Email, account.GoogleEmail);
        Assert.Equal(1, await _db.Accounts.CountAsync(a => a.Email == Email));   // no second account
    }

    [Fact]
    public async Task LinkingAnExistingAccount_PreservesItsPasswordAndRole()
    {
        // Linking must not downgrade a Teacher to Student or destroy their password login.
        var seeded = SeedAccount(roleId: TeacherRoleId, googleSubId: null);
        var originalHash = seeded.PasswordHash;
        GivenGoogleProfile();

        var result = await CallbackAsync();

        var account = await LoadByEmailAsync();
        Assert.Equal(originalHash, account.PasswordHash);
        Assert.Equal(TeacherRoleId, account.RoleId);
        Assert.Equal("Teacher", result.Value!.RoleName);
    }

    [Fact]
    public async Task LinkingAnExistingAccount_KeepsItsOriginalUsername()
    {
        SeedAccount(username: "original_name", googleSubId: null);
        GivenGoogleProfile(email: Email);

        var result = await CallbackAsync();

        Assert.Equal("original_name", result.Value!.Username);
    }

    // ---------------------------------------------------------------- deactivated account

    [Fact]
    public async Task DeactivatedAccount_IsRefused()
    {
        // UC-14: Google login must not be a way back into a deactivated account.
        SeedAccount(isActive: false, googleSubId: GoogleSub);
        GivenGoogleProfile();

        var result = await CallbackAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AccountDeactivated.Code, result.Error!.Code);
        VerifyNoTokensIssued();
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeactivatedUnlinkedAccount_IsRefusedWithoutBeingLinked()
    {
        SeedAccount(isActive: false, googleSubId: null);
        GivenGoogleProfile();

        var result = await CallbackAsync();

        Assert.True(result.IsFailure);
        var account = await LoadByEmailAsync();
        Assert.Null(account.GoogleSubId);   // the deactivation check runs before the link
    }

    // ---------------------------------------------------------------- session issuance

    [Fact]
    public async Task Success_ReturnsTheSameTokenPairShapeAsNormalLogin()
    {
        SeedAccount(googleSubId: GoogleSub);
        GivenGoogleProfile();

        var result = await CallbackAsync();

        var dto = result.Value!;
        Assert.Equal("acct-existing", dto.AccountId);
        Assert.Equal(Email, dto.Email);
        Assert.Equal("existing_user", dto.Username);
        Assert.Equal("Student", dto.RoleName);
        Assert.Equal(AccessToken, dto.AccessToken);
        Assert.Equal(RefreshToken, dto.RefreshToken);
        Assert.Equal(ExpiresAt, dto.ExpiresAt);
    }

    [Fact]
    public async Task Success_BindsRefreshTokenToTheAccessTokenJtiAndExpiry()
    {
        SeedAccount(googleSubId: GoogleSub);
        GivenGoogleProfile();

        await CallbackAsync();

        _tokenService.Verify(
            t => t.IssueRefreshTokenAsync("acct-existing", TokenId, ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StudentLogin_RevokesPreviousSessions()
    {
        // BR-02 applies on the Google path exactly as on the password path.
        SeedAccount(roleId: StudentRoleId, googleSubId: GoogleSub);
        GivenGoogleProfile();

        await CallbackAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync("acct-existing"), Times.Once);
    }

    [Fact]
    public async Task StudentLogin_RevokesBeforeIssuingTheNewRefreshToken()
    {
        SeedAccount(roleId: StudentRoleId, googleSubId: GoogleSub);
        GivenGoogleProfile();
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

        await CallbackAsync();

        Assert.Equal(new[] { "revoke", "issue" }, calls);
    }

    [Fact]
    public async Task NonStudentLogin_DoesNotRevokeSessions()
    {
        SeedAccount(roleId: TeacherRoleId, googleSubId: GoogleSub);
        GivenGoogleProfile();

        await CallbackAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------- misconfiguration & cancellation

    [Fact]
    public async Task MissingStudentRole_Throws()
    {
        // Auto-creation cannot invent a role: a deployment fault must fail loudly rather than
        // produce a role-less account.
        using var bareDb = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        foreach (var role in bareDb.Roles.ToList())
        {
            bareDb.Roles.Remove(role);
        }
        await bareDb.SaveChangesAsync();

        var handler = new GoogleOAuthCallbackCommandHandler(
            bareDb, _googleOAuth.Object, _tokenService.Object, _sessionService.Object, _publisher.Object);
        GivenGoogleProfile();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new GoogleOAuthCallbackCommand(Code), CancellationToken.None));
    }

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount(googleSubId: GoogleSub);
        GivenGoogleProfile();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CallbackAsync(cts.Token));
    }
}

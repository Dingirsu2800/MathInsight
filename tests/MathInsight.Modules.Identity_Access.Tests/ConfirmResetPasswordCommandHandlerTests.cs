using MathInsight.Modules.Identity_Access.Commands.ConfirmResetPassword;
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
/// Unit tests for ConfirmResetPasswordCommandHandler — UC-06 part 2, redeeming the reset token.
///
/// The rules that matter here: the token is single-use, it is bound to one account id, the new
/// password is stored as a BCrypt hash, and a successful reset revokes every session (BR-15).
/// IPasswordResetTokenStore and IAuthSessionService are Moq doubles (no Redis); EF Core InMemory
/// stands in for SQL Server. BCrypt runs for real so the hashing assertions mean something.
/// </summary>
public class ConfirmResetPasswordCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";

    private const string AccountId = "acct-A";
    private const string Email = "user-a@mathinsight.test";
    private const string Token = "reset-token-value";
    private const string NewPassword = "Brand#New1Password";
    private const string OldPasswordHash = "$2a$12$abcdefghijklmnopqrstuvwxyz012345678901234567890123456";

    private readonly IdentityDbContext _db;
    private readonly Mock<IPasswordResetTokenStore> _resetTokens = new();
    private readonly Mock<IAuthSessionService> _sessionService = new();
    private readonly ConfirmResetPasswordCommandHandler _handler;

    public ConfirmResetPasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);

        _handler = new ConfirmResetPasswordCommandHandler(_db, _resetTokens.Object, _sessionService.Object);
    }

    public void Dispose() => _db.Dispose();

    private void SeedAccount(
        string accountId = AccountId,
        string email = Email,
        string username = "user_a",
        string passwordHash = OldPasswordHash)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = accountId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = "Test",
            LastName = "User",
            RoleId = StudentRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Arms the token store to resolve <paramref name="token"/> to an account id.</summary>
    private void GivenValidToken(string accountId = AccountId, string token = Token) =>
        _resetTokens
            .Setup(s => s.GetAccountIdAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountId);

    private void GivenNoToken() =>
        _resetTokens
            .Setup(s => s.GetAccountIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

    private Task<MathInsight.Shared.Results.Result<MediatR.Unit>> ConfirmAsync(
        string token = Token,
        string newPassword = NewPassword,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(new ConfirmResetPasswordCommand(token, newPassword), cancellationToken);

    private Task<Account> LoadAsync(string accountId = AccountId) =>
        _db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == accountId);

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ValidToken_UpdatesThePassword()
    {
        SeedAccount();
        GivenValidToken();

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadAsync();
        Assert.NotEqual(OldPasswordHash, account.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash));
    }

    [Fact]
    public async Task ValidToken_StoresABCryptHashNeverThePlaintext()
    {
        // BR-08: the raw password must never reach the column.
        SeedAccount();
        GivenValidToken();

        await ConfirmAsync();

        var account = await LoadAsync();
        Assert.NotEqual(NewPassword, account.PasswordHash);
        Assert.StartsWith("$2", account.PasswordHash);
    }

    [Fact]
    public async Task ValidToken_ConsumesTheTokenSoItCannotBeReplayed()
    {
        SeedAccount();
        GivenValidToken();

        await ConfirmAsync();

        _resetTokens.Verify(s => s.DeleteAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplayedToken_FailsAfterTheFirstUse()
    {
        // End-to-end single-use behaviour: the store stops resolving the token once consumed.
        SeedAccount();
        var consumed = false;
        _resetTokens
            .Setup(s => s.GetAccountIdAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => consumed ? null : AccountId);
        _resetTokens
            .Setup(s => s.DeleteAsync(Token, It.IsAny<CancellationToken>()))
            .Callback(() => consumed = true)
            .Returns(Task.CompletedTask);

        var first = await ConfirmAsync();
        var replay = await ConfirmAsync(newPassword: "Second#Attempt1");

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsFailure);
        Assert.Equal(AuthErrors.TokenExpired.Code, replay.Error!.Code);

        // The replay must not have overwritten the password set by the first, successful reset.
        var account = await LoadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash));
    }

    [Fact]
    public async Task ValidToken_RevokesEveryExistingSession()
    {
        // BR-15: a reset must invalidate all outstanding access and refresh tokens.
        SeedAccount();
        GivenValidToken();

        await ConfirmAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task ValidToken_PersistsThePasswordBeforeRevokingSessions()
    {
        // Revoking first would log the user out of a reset that might still fail to save.
        SeedAccount();
        GivenValidToken();
        var passwordChangedAtRevokeTime = false;
        _sessionService
            .Setup(s => s.RevokeAllSessionsAsync(It.IsAny<string>()))
            .Callback(() => passwordChangedAtRevokeTime =
                _db.Accounts.AsNoTracking().Single(a => a.AccountId == AccountId).PasswordHash != OldPasswordHash)
            .Returns(Task.CompletedTask);

        await ConfirmAsync();

        Assert.True(passwordChangedAtRevokeTime);
    }

    [Fact]
    public async Task ValidToken_LeavesOtherAccountFieldsUntouched()
    {
        SeedAccount();
        GivenValidToken();

        await ConfirmAsync();

        var account = await LoadAsync();
        Assert.Equal(Email, account.Email);
        Assert.Equal("user_a", account.Username);
        Assert.True(account.IsActive);
        Assert.Equal(StudentRoleId, account.RoleId);
    }

    // ---------------------------------------------------------------- invalid tokens

    [Fact]
    public async Task MissingOrExpiredToken_ReturnsTokenExpired()
    {
        // A Redis miss covers "expired after 15 minutes" and "already used" alike.
        SeedAccount();
        GivenNoToken();

        var result = await ConfirmAsync(token: "unknown-token");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenExpired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MissingOrExpiredToken_ChangesNothing()
    {
        SeedAccount();
        GivenNoToken();

        await ConfirmAsync(token: "unknown-token");

        var account = await LoadAsync();
        Assert.Equal(OldPasswordHash, account.PasswordHash);   // password untouched
        _resetTokens.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TokenForAMissingAccount_IsRejectedAndSpent()
    {
        // The account was deleted after the token was issued: fail closed, and burn the token so
        // the dangling key cannot be retried.
        GivenValidToken(accountId: "deleted-account");

        var result = await ConfirmAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenExpired.Code, result.Error!.Code);
        _resetTokens.Verify(s => s.DeleteAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------- token/account binding

    [Fact]
    public async Task TokenResetsOnlyItsOwnAccount()
    {
        // A token minted for account B must never touch account A's credentials.
        SeedAccount(accountId: AccountId, email: Email, username: "user_a");
        SeedAccount(accountId: "acct-B", email: "user-b@mathinsight.test", username: "user_b");
        GivenValidToken(accountId: "acct-B");

        await ConfirmAsync();

        var victim = await LoadAsync(AccountId);
        Assert.Equal(OldPasswordHash, victim.PasswordHash);   // untouched

        var owner = await LoadAsync("acct-B");
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, owner.PasswordHash));
    }

    [Fact]
    public async Task TokenRevokesSessionsOnlyForItsOwnAccount()
    {
        SeedAccount(accountId: AccountId, email: Email, username: "user_a");
        SeedAccount(accountId: "acct-B", email: "user-b@mathinsight.test", username: "user_b");
        GivenValidToken(accountId: "acct-B");

        await ConfirmAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync("acct-B"), Times.Once);
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(AccountId), Times.Never);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount();
        GivenValidToken();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ConfirmAsync(cancellationToken: cts.Token));
    }
}

using MathInsight.Modules.Identity_Access.Commands.ResetPassword;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for ResetPasswordCommandHandler — UC-06 part 1, the "forgot password" request.
///
/// The defining rule is enumeration protection: the caller gets the same Success either way, and
/// the only observable difference between a known and an unknown email is whether the token store
/// and the mailer were touched at all. IPasswordResetTokenStore and IEmailService are Moq doubles
/// (no Redis, no SMTP); EF Core InMemory backs the account lookup.
/// </summary>
public class ResetPasswordCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";

    private const string AccountId = "acct-A";
    private const string Email = "user-a@mathinsight.test";
    private const string ResetToken = "reset-token-value";

    private readonly IdentityDbContext _db;
    private readonly Mock<IPasswordResetTokenStore> _resetTokens = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);

        _resetTokens
            .Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResetToken);

        _handler = new ResetPasswordCommandHandler(_db, _resetTokens.Object, _emailService.Object);
    }

    public void Dispose() => _db.Dispose();

    private void SeedAccount(string email = Email, string accountId = AccountId, string username = "user_a")
    {
        _db.Accounts.Add(new Account
        {
            AccountId = accountId,
            Username = username,
            Email = email,
            PasswordHash = "irrelevant-for-this-handler",
            FirstName = "Test",
            LastName = "User",
            RoleId = StudentRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Task<MathInsight.Shared.Results.Result<MediatR.Unit>> RequestResetAsync(
        string email = Email,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(new ResetPasswordCommand(email), cancellationToken);

    private void VerifyNoTokenIssuedOrEmailed()
    {
        _resetTokens.Verify(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(
            e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------- known account

    [Fact]
    public async Task KnownEmail_IssuesResetTokenForThatAccount()
    {
        SeedAccount();

        var result = await RequestResetAsync();

        Assert.True(result.IsSuccess);
        _resetTokens.Verify(s => s.CreateAsync(AccountId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KnownEmail_SendsResetEmailCarryingTheIssuedToken()
    {
        // The emailed token must be the one the store minted, or the link can never be redeemed.
        SeedAccount();

        await RequestResetAsync();

        _emailService.Verify(
            e => e.SendPasswordResetAsync(Email, ResetToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task KnownEmail_SendsToTheStoredAddressNotTheRequestedCasingOrPadding()
    {
        // The mail goes to account.Email — the address of record — never to raw caller input.
        SeedAccount();

        await RequestResetAsync(email: $"  {Email}  ");

        _emailService.Verify(
            e => e.SendPasswordResetAsync(Email, ResetToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task KnownEmail_IssuesTokenBeforeSendingEmail()
    {
        // A mail sent before the token exists would carry nothing redeemable.
        SeedAccount();
        var calls = new List<string>();
        _resetTokens
            .Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("create"))
            .ReturnsAsync(ResetToken);
        _emailService
            .Setup(e => e.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("email"))
            .Returns(Task.CompletedTask);

        await RequestResetAsync();

        Assert.Equal(new[] { "create", "email" }, calls);
    }

    [Fact]
    public async Task PaddedEmail_StillResolvesTheAccount()
    {
        SeedAccount();

        var result = await RequestResetAsync(email: $"   {Email}   ");

        Assert.True(result.IsSuccess);
        _resetTokens.Verify(s => s.CreateAsync(AccountId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepeatedRequests_IssueAFreshTokenEachTime()
    {
        // Requesting twice does not stack or reuse: each call mints a new token, and the store
        // decides what happens to the older key (it expires on its own 15-minute TTL).
        SeedAccount();
        _resetTokens
            .SetupSequence(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-1")
            .ReturnsAsync("token-2");

        await RequestResetAsync();
        await RequestResetAsync();

        _resetTokens.Verify(s => s.CreateAsync(AccountId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _emailService.Verify(
            e => e.SendPasswordResetAsync(Email, "token-1", It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(
            e => e.SendPasswordResetAsync(Email, "token-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KnownEmail_DoesNotModifyTheAccount()
    {
        // Requesting a reset must not itself change credentials or lock the account out.
        SeedAccount();

        await RequestResetAsync();

        var account = await _db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == AccountId);
        Assert.Equal("irrelevant-for-this-handler", account.PasswordHash);
        Assert.True(account.IsActive);
    }

    // ---------------------------------------------------------------- unknown account

    [Fact]
    public async Task UnknownEmail_StillReturnsSuccess()
    {
        // UC-06 enumeration protection: the caller cannot tell the difference.
        var result = await RequestResetAsync(email: "ghost@mathinsight.test");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnknownEmail_IssuesNoTokenAndSendsNoEmail()
    {
        await RequestResetAsync(email: "ghost@mathinsight.test");

        VerifyNoTokenIssuedOrEmailed();
    }

    [Fact]
    public async Task KnownAndUnknownEmail_ProduceIdenticalResults()
    {
        // The response itself must carry no signal — same success, same absent error.
        SeedAccount();

        var known = await RequestResetAsync();
        var unknown = await RequestResetAsync(email: "ghost@mathinsight.test");

        Assert.Equal(known.IsSuccess, unknown.IsSuccess);
        Assert.Equal(known.IsFailure, unknown.IsFailure);
        Assert.Null(known.Error);
        Assert.Null(unknown.Error);
    }

    [Fact]
    public async Task EmailOfAnotherAccount_IssuesTokenOnlyForThatAccount()
    {
        // The token is bound to the account that owns the address, never to the caller.
        SeedAccount(email: Email, accountId: AccountId, username: "user_a");
        SeedAccount(email: "user-b@mathinsight.test", accountId: "acct-B", username: "user_b");

        await RequestResetAsync(email: "user-b@mathinsight.test");

        _resetTokens.Verify(s => s.CreateAsync("acct-B", It.IsAny<CancellationToken>()), Times.Once);
        _resetTokens.Verify(s => s.CreateAsync(AccountId, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RequestResetAsync(cancellationToken: cts.Token));
    }
}

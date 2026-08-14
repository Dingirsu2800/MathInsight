using MathInsight.Modules.Identity_Access.Commands.Login;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
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
/// Unit tests for LoginCommandHandler (UC-01, BR-02/BR-03/BR-06, DD-01/DD-02).
///
/// EF Core InMemory stands in for SQL Server (mirroring the other module test projects); the two
/// collaborators the handler owns — <see cref="ITokenService"/> and <see cref="IAuthSessionService"/>
/// — are Moq doubles, so no Redis and no JWT signing key are involved. Passwords are hashed once
/// with the real BCrypt so the verification branch is exercised for real rather than stubbed.
/// </summary>
public class LoginCommandHandlerTests : IDisposable
{
    private const string CorrectPassword = "Correct#Password1";
    private const string WrongPassword = "Wrong#Password1";

    // Role ids match the seeded roles in RoleConfiguration.HasData.
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";
    private const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";
    private const string ExpertRoleId = "22222222-2222-2222-2222-222222222222";

    private const string AccountId = "acct-A";
    private const string Email = "user-a@mathinsight.test";
    private const string Username = "user_a";

    private const string AccessToken = "access-token-value";
    private const string RefreshToken = "refresh-token-value";
    private const string TokenId = "jti-0001";
    private static readonly DateTime ExpiresAt = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    // BCrypt is deliberately slow; hash once for the whole class.
    private static readonly string CorrectPasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword);

    private readonly IdentityDbContext _db;
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IAuthSessionService> _sessionService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        EnsureRole(StudentRoleId, "Student");
        EnsureRole(TeacherRoleId, "Teacher");
        EnsureRole(ExpertRoleId, "Expert");

        // Moq needs concrete locals to fill the two `out` parameters of CreateAccessToken.
        var expiresAt = ExpiresAt;
        var tokenId = TokenId;
        _tokenService
            .Setup(t => t.CreateAccessToken(It.IsAny<Account>(), out expiresAt, out tokenId))
            .Returns(AccessToken);
        _tokenService
            .Setup(t => t.IssueRefreshTokenAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshToken);
        _sessionService.Setup(s => s.IsLockedAsync(It.IsAny<string>())).ReturnsAsync(false);

        _handler = new LoginCommandHandler(_db, _tokenService.Object, _sessionService.Object);
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

    private Role SeedRole(string roleId, string roleName)
    {
        var role = new Role { RoleId = roleId, RoleName = roleName };
        _db.Roles.Add(role);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return role;
    }

    private void SeedAccount(
        string roleId = StudentRoleId,
        bool isActive = true,
        string accountId = AccountId,
        string email = Email,
        string username = Username,
        string? passwordHash = null)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = accountId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash ?? CorrectPasswordHash,
            FirstName = "Test",
            LastName = "User",
            RoleId = roleId,
            IsActive = isActive,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void SeedApplication(string status, DateTime appliedTime, string teacherId = AccountId)
    {
        _db.TeacherApplications.Add(new TeacherApplication
        {
            ApplicationId = Guid.NewGuid().ToString(),
            TeacherId = teacherId,
            DocumentsUrl = "https://cdn.test/certificate.png",
            Status = status,
            AppliedTime = appliedTime
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Task<MathInsight.Shared.Results.Result<LoginResponse>> LoginAsync(
        string usernameOrEmail = Email,
        string password = CorrectPassword,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(new LoginCommand(usernameOrEmail, password), cancellationToken);

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

    // ---------------------------------------------------------------- happy paths

    [Fact]
    public async Task ValidCredentials_Student_ReturnsTokensAndProfile()
    {
        SeedAccount();

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(AccountId, dto.AccountId);
        Assert.Equal(Email, dto.Email);
        Assert.Equal(Username, dto.Username);
        Assert.Equal("Student", dto.RoleName);
        Assert.Equal(AccessToken, dto.AccessToken);
        Assert.Equal(RefreshToken, dto.RefreshToken);
        Assert.Equal(ExpiresAt, dto.ExpiresAt);
        Assert.Null(dto.ApplicationStatus);   // not a Teacher → field is meaningless
    }

    [Fact]
    public async Task ValidCredentials_ByUsername_Succeeds()
    {
        // Second arm of the lookup predicate (Email == input || Username == input).
        SeedAccount();

        var result = await LoginAsync(usernameOrEmail: Username);

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountId, result.Value!.AccountId);
    }

    [Fact]
    public async Task UsernameOrEmail_SurroundingWhitespace_IsTrimmed()
    {
        SeedAccount();

        var result = await LoginAsync(usernameOrEmail: $"  {Email}  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(AccountId, result.Value!.AccountId);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailedLoginCounter()
    {
        SeedAccount();

        await LoginAsync();

        _sessionService.Verify(s => s.ResetFailedLoginAsync(AccountId), Times.Once);
        _sessionService.Verify(s => s.RecordFailedLoginAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulLogin_BindsRefreshTokenToAccessTokenJtiAndExpiry()
    {
        // DD-02: the refresh token must be minted against the jti and expiry of the access token
        // just created, otherwise revocation cannot blacklist the right access token later.
        SeedAccount();

        await LoginAsync();

        _tokenService.Verify(
            t => t.IssueRefreshTokenAsync(AccountId, TokenId, ExpiresAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------- BR-02 single session

    [Fact]
    public async Task Student_Login_RevokesPreviousSessions()
    {
        SeedAccount(roleId: StudentRoleId);

        await LoginAsync();

        _sessionService.Verify(s => s.RevokeAllSessionsAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task Student_Login_RevokesSessionsBeforeIssuingNewRefreshToken()
    {
        // Ordering is the whole point of BR-02: revoking after issuing would kill the new session.
        SeedAccount(roleId: StudentRoleId);
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

        await LoginAsync();

        Assert.Equal(new[] { "revoke", "issue" }, calls);
    }

    [Fact]
    public async Task NonStudent_Login_DoesNotRevokeSessions()
    {
        // BR-02 is Student-only; an Expert may hold concurrent sessions.
        SeedAccount(roleId: ExpertRoleId);

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Expert", result.Value!.RoleName);
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RoleName_IsMatchedCaseInsensitively()
    {
        // IsRole uses OrdinalIgnoreCase, so a differently-cased role row still triggers BR-02.
        var lowercaseStudent = SeedRole("55555555-5555-5555-5555-555555555555", "student");
        SeedAccount(roleId: lowercaseStudent.RoleId);

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(AccountId), Times.Once);
    }

    // ---------------------------------------------------------------- credential failures

    [Fact]
    public async Task NonExistentAccount_ReturnsInvalidCredentials_WithoutCountingAFailure()
    {
        // No account → nothing to lock out, and no token may be minted.
        var result = await LoginAsync(usernameOrEmail: "ghost@mathinsight.test");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error!.Code);
        _sessionService.Verify(s => s.RecordFailedLoginAsync(It.IsAny<string>()), Times.Never);
        _sessionService.Verify(s => s.ResetFailedLoginAsync(It.IsAny<string>()), Times.Never);
        VerifyNoTokensIssued();
    }

    [Fact]
    public async Task WrongPassword_ReturnsInvalidCredentials_AndRecordsFailedLogin()
    {
        SeedAccount();

        var result = await LoginAsync(password: WrongPassword);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error!.Code);
        _sessionService.Verify(s => s.RecordFailedLoginAsync(AccountId), Times.Once);
        _sessionService.Verify(s => s.ResetFailedLoginAsync(It.IsAny<string>()), Times.Never);
        VerifyNoTokensIssued();
    }

    [Fact]
    public async Task WrongPassword_AndUnknownAccount_AreIndistinguishable()
    {
        // BR-03 anti-enumeration: identical code AND message for both.
        SeedAccount();

        var wrongPassword = await LoginAsync(password: WrongPassword);
        var unknownAccount = await LoginAsync(usernameOrEmail: "ghost@mathinsight.test");

        Assert.Equal(unknownAccount.Error!.Code, wrongPassword.Error!.Code);
        Assert.Equal(unknownAccount.Error!.Message, wrongPassword.Error!.Message);
    }

    [Fact]
    public async Task LockedAccount_IsRejected_EvenWithCorrectPassword()
    {
        // The lock check runs before password verification, so a locked account cannot be probed
        // and its counters are left untouched.
        SeedAccount();
        _sessionService.Setup(s => s.IsLockedAsync(AccountId)).ReturnsAsync(true);

        var result = await LoginAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AccountLocked.Code, result.Error!.Code);
        _sessionService.Verify(s => s.RecordFailedLoginAsync(It.IsAny<string>()), Times.Never);
        _sessionService.Verify(s => s.ResetFailedLoginAsync(It.IsAny<string>()), Times.Never);
        VerifyNoTokensIssued();
    }

    // ---------------------------------------------------------------- DD-01 deactivation

    [Fact]
    public async Task DeactivatedAccount_CorrectPassword_ReturnsAccountDeactivated()
    {
        SeedAccount(isActive: false);

        var result = await LoginAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AccountDeactivated.Code, result.Error!.Code);
        VerifyNoTokensIssued();
        _sessionService.Verify(s => s.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeactivatedAccount_StillClearsFailedLoginCounter()
    {
        // Documents the ordering: the counter is reset as soon as the password is proven correct,
        // before the is_active check — so a deactivated user is never also locked out.
        SeedAccount(isActive: false);

        await LoginAsync();

        _sessionService.Verify(s => s.ResetFailedLoginAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task DeactivatedAccount_WrongPassword_StillReportsInvalidCredentials()
    {
        // Deactivation must not leak: a wrong password fails identically whether or not the
        // account is active.
        SeedAccount(isActive: false);

        var result = await LoginAsync(password: WrongPassword);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error!.Code);
    }

    // ---------------------------------------------------------------- BR-06 teacher application

    [Fact]
    public async Task Teacher_ApprovedApplication_LogsInWithApprovedStatus()
    {
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication(TeacherApplication.StatusApproved, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginApplicationStatus.Approved, result.Value!.ApplicationStatus);
        Assert.Equal(AccessToken, result.Value.AccessToken);
    }

    [Fact]
    public async Task Teacher_PendingApplication_LoginAllowed_WithPendingStatus()
    {
        // Login is NOT gated on approval: a pending applicant signs in and is routed by the client
        // using ApplicationStatus. Teacher features are gated per request by the TeacherApproved policy.
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication(TeacherApplication.StatusPending, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginApplicationStatus.Pending, result.Value!.ApplicationStatus);
        Assert.Equal(AccessToken, result.Value.AccessToken);
    }

    [Fact]
    public async Task Teacher_RejectedApplication_LoginAllowed_WithRejectedStatus()
    {
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication(TeacherApplication.StatusRejected, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginApplicationStatus.Rejected, result.Value!.ApplicationStatus);
        Assert.Equal(AccessToken, result.Value.AccessToken);
    }

    [Fact]
    public async Task Teacher_WithNoApplicationRow_ReturnsNone()
    {
        // UC-11: Admin-created Teacher never filed an application — a normal login, not "pending".
        SeedAccount(roleId: TeacherRoleId);

        var result = await LoginAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(LoginApplicationStatus.None, result.Value!.ApplicationStatus);
    }

    [Fact]
    public async Task Teacher_MultipleApplications_UsesLatestByAppliedTime()
    {
        // A re-application after a rejection must surface the newest decision, not the first row.
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication(TeacherApplication.StatusRejected, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedApplication(TeacherApplication.StatusApproved, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.Equal(LoginApplicationStatus.Approved, result.Value!.ApplicationStatus);
    }

    [Fact]
    public async Task Teacher_StatusCasing_IsIgnored()
    {
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication("APPROVED", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.Equal(LoginApplicationStatus.Approved, result.Value!.ApplicationStatus);
    }

    [Fact]
    public async Task Teacher_UnrecognisedStatus_FallsBackToPending()
    {
        // Fail closed: an out-of-vocabulary status is reported as unapproved, never as approved.
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication("Withdrawn", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await LoginAsync();

        Assert.Equal(LoginApplicationStatus.Pending, result.Value!.ApplicationStatus);
    }

    [Fact]
    public async Task Teacher_ApplicationOfAnotherTeacher_IsIgnored()
    {
        // The status query filters on the caller's account id.
        SeedAccount(roleId: TeacherRoleId);
        SeedApplication(TeacherApplication.StatusApproved,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            teacherId: "some-other-teacher");

        var result = await LoginAsync();

        Assert.Equal(LoginApplicationStatus.None, result.Value!.ApplicationStatus);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LoginAsync(cancellationToken: cts.Token));
    }
}

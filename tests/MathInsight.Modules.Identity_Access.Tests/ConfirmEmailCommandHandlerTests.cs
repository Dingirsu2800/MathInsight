using MathInsight.Modules.Identity_Access.Commands.ConfirmEmail;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for ConfirmEmailCommandHandler (UC-93, BR-04 step 5, BR-05, DD-01).
///
/// This is the only place a self-registered Account row is created, so the assertions are mostly
/// about what lands in the database and what is published afterwards. The pending-registration
/// store (Redis in production) and the MediatR publisher are Moq doubles; EF Core InMemory stands
/// in for SQL Server, mirroring the other module test projects.
/// </summary>
public class ConfirmEmailCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";
    private const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";

    private const string Token = "confirmation-token-value";
    private const string Email = "new-student@mathinsight.test";
    private const string Username = "new_student";
    private const string PasswordHash = "$2a$12$abcdefghijklmnopqrstuvwxyz012345678901234567890123456";

    private readonly IdentityDbContext _db;
    private readonly Mock<IPendingRegistrationStore> _store = new();
    private readonly Mock<IPublisher> _publisher = new();
    private readonly ConfirmEmailCommandHandler _handler;

    public ConfirmEmailCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        EnsureRole(StudentRoleId, "Student");
        EnsureRole(TeacherRoleId, "Teacher");

        _handler = new ConfirmEmailCommandHandler(_db, _store.Object, _publisher.Object);
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

    private static PendingRegistration StudentPayload(
        string email = Email,
        string username = Username,
        string? phoneNumber = null) =>
        new()
        {
            Username = username,
            Email = email,
            PasswordHash = PasswordHash,
            Role = "Student",
            FirstName = "New",
            LastName = "Student",
            PhoneNumber = phoneNumber,
            Gender = "Female",
            School = "Le Quy Don High School",
            CurrentGrade = 11
        };

    private static PendingRegistration TeacherPayload(
        string? documentsUrl = "https://cdn.test/cert-1.png",
        IReadOnlyList<string>? documentsUrls = null,
        string? phoneNumber = "0900000001") =>
        new()
        {
            Username = "new_teacher",
            Email = "new-teacher@mathinsight.test",
            PasswordHash = PasswordHash,
            Role = "Teacher",
            FirstName = "New",
            LastName = "Teacher",
            PhoneNumber = phoneNumber,
            Biography = "Ten years of calculus.",
            DocumentsUrl = documentsUrl,
            DocumentsUrls = documentsUrls
        };

    private void GivenPendingRegistration(PendingRegistration payload, string token = Token) =>
        _store
            .Setup(s => s.GetAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

    private void SeedAccount(
        string email = "existing@mathinsight.test",
        string username = "existing_user",
        string? phoneNumber = null)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            PasswordHash = PasswordHash,
            FirstName = "Existing",
            LastName = "User",
            PhoneNumber = phoneNumber,
            RoleId = StudentRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Task<MathInsight.Shared.Results.Result<Unit>> ConfirmAsync(
        string token = Token,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(new ConfirmEmailCommand(token), cancellationToken);

    private Task<Account?> LoadAccountAsync(string email) =>
        _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email);

    private void VerifyNothingCommitted()
    {
        _publisher.Verify(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(
            p => p.Publish(It.IsAny<TeacherApplicationSubmittedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _store.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- invalid / expired token

    [Fact]
    public async Task MissingPayload_ReturnsTokenExpired()
    {
        // Redis miss covers both "expired after 24h" and "already consumed" — indistinguishable.
        _store
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        var result = await ConfirmAsync("unknown-token");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenExpired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MissingPayload_CreatesNoRowsAndPublishesNothing()
    {
        _store
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        await ConfirmAsync("unknown-token");

        Assert.False(await _db.Accounts.AnyAsync());
        Assert.False(await _db.Students.AnyAsync());
        VerifyNothingCommitted();
    }

    // ---------------------------------------------------------------- student happy path

    [Fact]
    public async Task ValidStudentToken_CreatesActiveAccountWithStudentRole()
    {
        GivenPendingRegistration(StudentPayload());

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(Email);
        Assert.NotNull(account);
        Assert.Equal(Username, account!.Username);
        Assert.Equal(PasswordHash, account.PasswordHash);   // hash carried over verbatim
        Assert.Equal("New", account.FirstName);
        Assert.Equal("Student", account.LastName);
        Assert.Equal(StudentRoleId, account.RoleId);
        Assert.True(account.IsActive);                      // DD-01: persisted ⇒ confirmed
        Assert.True(Guid.TryParse(account.AccountId, out _));
    }

    [Fact]
    public async Task ValidStudentToken_CreatesStudentRowLinkedToTheAccount()
    {
        GivenPendingRegistration(StudentPayload());

        await ConfirmAsync();

        var account = await LoadAccountAsync(Email);
        var student = await _db.Students.AsNoTracking().SingleAsync();
        Assert.Equal(account!.AccountId, student.StudentId);   // Student PK is the Account id
        Assert.Equal("Female", student.Gender);
        Assert.Equal("Le Quy Don High School", student.School);
        Assert.Equal(11, student.CurrentGrade);
    }

    [Fact]
    public async Task ValidStudentToken_CreatesNoTeacherRows()
    {
        GivenPendingRegistration(StudentPayload());

        await ConfirmAsync();

        Assert.False(await _db.Teachers.AnyAsync());
        Assert.False(await _db.TeacherApplications.AnyAsync());
    }

    [Fact]
    public async Task ValidStudentToken_PublishesAccountCreatedEventOnly()
    {
        AccountCreatedEvent? published = null;
        _publisher
            .Setup(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AccountCreatedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);
        GivenPendingRegistration(StudentPayload());

        await ConfirmAsync();

        Assert.NotNull(published);
        Assert.Equal(Email, published!.Email);
        Assert.Equal(Username, published.Username);
        Assert.Equal("Student", published.RoleName);
        _publisher.Verify(
            p => p.Publish(It.IsAny<TeacherApplicationSubmittedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidToken_PublishesOnlyAfterTheWriteIsDurable()
    {
        // "Publish only after the write is durable": a subscriber that reads the account back must
        // find it. A not-yet-saved entity is invisible to a query, so this probe is meaningful.
        var accountVisibleAtPublishTime = false;
        _publisher
            .Setup(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => accountVisibleAtPublishTime = _db.Accounts.Any(a => a.Email == Email))
            .Returns(Task.CompletedTask);
        GivenPendingRegistration(StudentPayload());

        await ConfirmAsync();

        Assert.True(accountVisibleAtPublishTime);
    }

    [Fact]
    public async Task ValidToken_ConsumesThePendingRegistration()
    {
        // One-time use: the Redis key is deleted so a replayed link fails as TokenExpired.
        GivenPendingRegistration(StudentPayload());

        await ConfirmAsync();

        _store.Verify(s => s.DeleteAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- confirmation-time races

    [Fact]
    public async Task EmailClaimedSinceRegistration_ReturnsAlreadyConfirmed()
    {
        // The pending-registration race: a second registration for the same email confirmed first.
        SeedAccount(email: Email, username: "someone_else");
        GivenPendingRegistration(StudentPayload());

        var result = await ConfirmAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        Assert.Equal(1, await _db.Accounts.CountAsync());   // no second row
        VerifyNothingCommitted();
    }

    [Fact]
    public async Task UsernameClaimedSinceRegistration_ReturnsAlreadyConfirmed()
    {
        SeedAccount(email: "someone.else@mathinsight.test", username: Username);
        GivenPendingRegistration(StudentPayload());

        var result = await ConfirmAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        Assert.Equal(1, await _db.Accounts.CountAsync());
        VerifyNothingCommitted();
    }

    [Fact]
    public async Task PhoneNumberClaimedSinceRegistration_ReturnsPhoneAlreadyUsed()
    {
        // Account.PhoneNumber is uniquely indexed where not null; without this re-check the clash
        // would surface as a failed insert.
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: "0900000001");
        GivenPendingRegistration(TeacherPayload(phoneNumber: "0900000001"));

        var result = await ConfirmAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PhoneNumberAlreadyUsed.Code, result.Error!.Code);
        Assert.Equal(1, await _db.Accounts.CountAsync());
        VerifyNothingCommitted();
    }

    [Fact]
    public async Task NullPhoneNumber_SkipsThePhoneCheck()
    {
        // Guard on null matters: an unguarded `PhoneNumber == null` query would match every
        // existing account without a phone and wrongly fail every student confirmation.
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: null);
        GivenPendingRegistration(StudentPayload());   // student payloads carry no phone

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(await LoadAccountAsync(Email));
    }

    [Fact]
    public async Task WhitespacePhoneNumber_SkipsThePhoneCheck()
    {
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: "   ");
        GivenPendingRegistration(StudentPayload(phoneNumber: "   "));

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnusedPhoneNumber_IsPersistedOnTheAccount()
    {
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: "0900000002");
        GivenPendingRegistration(StudentPayload(phoneNumber: "0900000001"));

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync(Email);
        Assert.Equal("0900000001", account!.PhoneNumber);
    }

    // ---------------------------------------------------------------- teacher path

    [Fact]
    public async Task ValidTeacherToken_CreatesTeacherAndPendingApplication()
    {
        GivenPendingRegistration(TeacherPayload());

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync("new-teacher@mathinsight.test");
        Assert.Equal(TeacherRoleId, account!.RoleId);

        var teacher = await _db.Teachers.AsNoTracking().SingleAsync();
        Assert.Equal(account.AccountId, teacher.TeacherId);
        Assert.Equal("Ten years of calculus.", teacher.Biography);
        Assert.False(teacher.IsVerified);   // BR-06: verification is the Admin's decision

        var application = await _db.TeacherApplications.AsNoTracking().SingleAsync();
        Assert.Equal(account.AccountId, application.TeacherId);
        Assert.Equal(TeacherApplication.StatusPending, application.Status);   // title case for the CHECK constraint
        Assert.Equal("https://cdn.test/cert-1.png", application.DocumentsUrl);
    }

    [Fact]
    public async Task ValidTeacherToken_CreatesNoStudentRow()
    {
        GivenPendingRegistration(TeacherPayload());

        await ConfirmAsync();

        Assert.False(await _db.Students.AnyAsync());
    }

    [Fact]
    public async Task ValidTeacherToken_PublishesBothEvents()
    {
        TeacherApplicationSubmittedEvent? submitted = null;
        _publisher
            .Setup(p => p.Publish(It.IsAny<TeacherApplicationSubmittedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TeacherApplicationSubmittedEvent, CancellationToken>((e, _) => submitted = e)
            .Returns(Task.CompletedTask);
        GivenPendingRegistration(TeacherPayload());

        await ConfirmAsync();

        _publisher.Verify(p => p.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(submitted);
        var application = await _db.TeacherApplications.AsNoTracking().SingleAsync();
        Assert.Equal(application.ApplicationId, submitted!.ApplicationId);
        Assert.Equal(application.TeacherId, submitted.TeacherId);
        Assert.Equal("new-teacher@mathinsight.test", submitted.Email);
    }

    [Fact]
    public async Task TeacherWithMultipleCertificates_JoinsThemWithTheSeparator()
    {
        // BR-05 multi-upload: every URL is kept, newline-separated in the single DocumentsUrl column.
        GivenPendingRegistration(TeacherPayload(documentsUrls:
            ["https://cdn.test/a.png", "https://cdn.test/b.png", "https://cdn.test/c.png"]));

        await ConfirmAsync();

        var application = await _db.TeacherApplications.AsNoTracking().SingleAsync();
        Assert.Equal(
            string.Join(TeacherApplication.DocumentsUrlSeparator,
                "https://cdn.test/a.png", "https://cdn.test/b.png", "https://cdn.test/c.png"),
            application.DocumentsUrl);
    }

    [Fact]
    public async Task TeacherWithNullDocumentsUrls_FallsBackToTheSingleUrl()
    {
        // Payloads written to Redis before multi-upload existed must still confirm.
        GivenPendingRegistration(TeacherPayload(documentsUrl: "https://cdn.test/legacy.png", documentsUrls: null));

        await ConfirmAsync();

        var application = await _db.TeacherApplications.AsNoTracking().SingleAsync();
        Assert.Equal("https://cdn.test/legacy.png", application.DocumentsUrl);
    }

    [Fact]
    public async Task TeacherWithEmptyDocumentsUrls_FallsBackToTheSingleUrl()
    {
        // Count == 0 must take the fallback, not produce an empty DocumentsUrl.
        GivenPendingRegistration(TeacherPayload(documentsUrl: "https://cdn.test/legacy.png", documentsUrls: []));

        await ConfirmAsync();

        var application = await _db.TeacherApplications.AsNoTracking().SingleAsync();
        Assert.Equal("https://cdn.test/legacy.png", application.DocumentsUrl);
    }

    // ---------------------------------------------------------------- misconfiguration & cancellation

    [Fact]
    public async Task UnknownRoleInPayload_Throws()
    {
        // A payload naming a role that was never seeded is a deployment fault, not a user error:
        // it must fail loudly rather than create a role-less account.
        GivenPendingRegistration(StudentPayload() with { Role = "Wizard" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmAsync());

        Assert.Contains("Wizard", exception.Message);
        Assert.False(await _db.Accounts.AnyAsync(a => a.Email == Email));
    }

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        GivenPendingRegistration(StudentPayload());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConfirmAsync(cancellationToken: cts.Token));
    }
}

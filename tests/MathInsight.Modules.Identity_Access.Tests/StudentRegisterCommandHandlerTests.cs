using MathInsight.Modules.Identity_Access.Commands.Register;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for StudentRegisterCommandHandler (UC-39, BR-04, BR-08, DD-01).
///
/// The defining rule is DD-01: this handler writes NOTHING to SQL — the payload lives in the
/// pending-registration store (Redis in production) until confirmation. The store and the email
/// service are Moq doubles, so no Redis and no SMTP are involved; EF Core InMemory backs only the
/// uniqueness read. BCrypt runs for real so the BR-08 hashing assertion means something.
/// </summary>
public class StudentRegisterCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";

    private const string Email = "new-student@mathinsight.test";
    private const string Username = "new_student";
    private const string Password = "Str0ng#Password";
    private const string Token = "confirmation-token-value";

    private readonly IdentityDbContext _db;
    private readonly Mock<IPendingRegistrationStore> _store = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly StudentRegisterCommandHandler _handler;

    public StudentRegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);

        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Token);

        _handler = new StudentRegisterCommandHandler(_db, _store.Object, _emailService.Object);
    }

    public void Dispose() => _db.Dispose();

    private void SeedAccount(string email, string username)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            PasswordHash = "irrelevant-for-this-handler",
            FirstName = "Existing",
            LastName = "User",
            RoleId = StudentRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private PendingRegistration? _capturedPayload;

    /// <summary>Arms the store double to record the payload it is handed.</summary>
    private void CapturePayload() =>
        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<PendingRegistration, CancellationToken>((payload, _) => _capturedPayload = payload)
            .ReturnsAsync(Token);

    private Task<MathInsight.Shared.Results.Result<MediatR.Unit>> RegisterAsync(
        string email = Email,
        string username = Username,
        string password = Password,
        string firstName = "New",
        string lastName = "Student",
        string? gender = "Female",
        string? school = "Le Quy Don High School",
        int? currentGrade = 11,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(
            new StudentRegisterCommand(username, email, password, firstName, lastName, gender, school, currentGrade),
            cancellationToken);

    private void VerifyNothingPersistedOrSent()
    {
        _store.Verify(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(
            e => e.SendRegistrationConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------- DD-01 happy path

    [Fact]
    public async Task ValidRegistration_Succeeds()
    {
        var result = await RegisterAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidRegistration_WritesNoAccountRow()
    {
        // DD-01, the whole point of the two-step flow: nothing reaches SQL until confirmation.
        await RegisterAsync();

        Assert.False(await _db.Accounts.AnyAsync(a => a.Email == Email || a.Username == Username));
        Assert.False(await _db.Students.AnyAsync());
    }

    [Fact]
    public async Task ValidRegistration_StoresPayloadInPendingStore()
    {
        CapturePayload();

        await RegisterAsync();

        Assert.NotNull(_capturedPayload);
        Assert.Equal(Email, _capturedPayload!.Email);
        Assert.Equal(Username, _capturedPayload.Username);
        Assert.Equal("New", _capturedPayload.FirstName);
        Assert.Equal("Student", _capturedPayload.LastName);
    }

    [Fact]
    public async Task ValidRegistration_AlwaysStoresStudentRole()
    {
        // The role is fixed by the handler, never taken from the request.
        CapturePayload();

        await RegisterAsync();

        Assert.Equal("Student", _capturedPayload!.Role);
    }

    [Fact]
    public async Task ValidRegistration_CarriesStudentProfileFields()
    {
        CapturePayload();

        await RegisterAsync(gender: "Male", school: "Chu Van An", currentGrade: 12);

        Assert.Equal("Male", _capturedPayload!.Gender);
        Assert.Equal("Chu Van An", _capturedPayload.School);
        Assert.Equal(12, _capturedPayload.CurrentGrade);
        Assert.Null(_capturedPayload.PhoneNumber);   // students supply no phone at registration
    }

    [Fact]
    public async Task ValidRegistration_HashesPasswordAndNeverStoresPlaintext()
    {
        // BR-08: only a BCrypt hash may leave this handler.
        CapturePayload();

        await RegisterAsync();

        Assert.NotEqual(Password, _capturedPayload!.PasswordHash);
        Assert.StartsWith("$2", _capturedPayload.PasswordHash);                          // BCrypt marker
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, _capturedPayload.PasswordHash));   // and it verifies
    }

    [Fact]
    public async Task ValidRegistration_SendsConfirmationEmailWithTheStoredToken()
    {
        // The emailed token must be the one the store minted, or confirmation can never succeed.
        await RegisterAsync();

        _emailService.Verify(
            e => e.SendRegistrationConfirmationAsync(Email, Token, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidRegistration_StoresPayloadBeforeSendingEmail()
    {
        // A mail that arrives before the payload is stored would produce an unusable link.
        var calls = new List<string>();
        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .ReturnsAsync(Token);
        _emailService
            .Setup(e => e.SendRegistrationConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("email"))
            .Returns(Task.CompletedTask);

        await RegisterAsync();

        Assert.Equal(new[] { "save", "email" }, calls);
    }

    // ---------------------------------------------------------------- input normalisation

    [Fact]
    public async Task Registration_TrimsEmailUsernameAndNames()
    {
        CapturePayload();

        await RegisterAsync(
            email: $"  {Email}  ",
            username: $"  {Username}  ",
            firstName: "  New  ",
            lastName: "  Student  ");

        Assert.Equal(Email, _capturedPayload!.Email);
        Assert.Equal(Username, _capturedPayload.Username);
        Assert.Equal("New", _capturedPayload.FirstName);
        Assert.Equal("Student", _capturedPayload.LastName);
    }

    [Fact]
    public async Task Registration_SendsEmailToTheTrimmedAddress()
    {
        await RegisterAsync(email: $"  {Email}  ");

        _emailService.Verify(
            e => e.SendRegistrationConfirmationAsync(Email, Token, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PaddedEmail_StillDetectedAsDuplicate()
    {
        // The uniqueness check must run on the trimmed value, or padding would bypass it.
        SeedAccount(Email, "someone_else");

        var result = await RegisterAsync(email: $"   {Email}   ");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingPersistedOrSent();
    }

    // ---------------------------------------------------------------- BR-04 uniqueness

    [Fact]
    public async Task DuplicateEmail_IsRejected()
    {
        SeedAccount(Email, "someone_else");

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingPersistedOrSent();
    }

    [Fact]
    public async Task DuplicateUsername_IsRejected()
    {
        SeedAccount("someone.else@mathinsight.test", Username);

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingPersistedOrSent();
    }

    [Fact]
    public async Task UnrelatedExistingAccount_DoesNotBlockRegistration()
    {
        // Negative control for the uniqueness predicate: a different identity must not collide.
        SeedAccount("other@mathinsight.test", "other_user");

        var result = await RegisterAsync();

        Assert.True(result.IsSuccess);
        _store.Verify(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RegisterAsync(cancellationToken: cts.Token));
    }
}

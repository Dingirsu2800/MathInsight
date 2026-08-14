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
/// Unit tests for TeacherRegisterCommandHandler (UC-08, BR-04, BR-05, BR-08, DD-01).
///
/// Same shape as StudentRegisterCommandHandlerTests, plus the two things teacher registration adds:
/// a phone-number uniqueness check and certificate uploads. DD-01 still governs — no SQL insert
/// happens here. ICertificateStorage, IPendingRegistrationStore and IEmailService are Moq doubles,
/// so no blob storage, no Redis and no SMTP are involved; BCrypt runs for real.
/// </summary>
public class TeacherRegisterCommandHandlerTests : IDisposable
{
    private const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";

    private const string Email = "new-teacher@mathinsight.test";
    private const string Username = "new_teacher";
    private const string Password = "Str0ng#Password";
    private const string PhoneNumber = "0900000001";
    private const string Token = "confirmation-token-value";
    private const string UploadedUrl = "https://cdn.test/teacher-certificates/cert-1.png";

    private readonly IdentityDbContext _db;
    private readonly Mock<IPendingRegistrationStore> _store = new();
    private readonly Mock<ICertificateStorage> _certificateStorage = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly TeacherRegisterCommandHandler _handler;

    private PendingRegistration? _capturedPayload;

    public TeacherRegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);

        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Token);
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadedUrl);

        _handler = new TeacherRegisterCommandHandler(
            _db, _store.Object, _certificateStorage.Object, _emailService.Object);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Arms the store double to record the payload it is handed.</summary>
    private void CapturePayload() =>
        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<PendingRegistration, CancellationToken>((payload, _) => _capturedPayload = payload)
            .ReturnsAsync(Token);

    /// <summary>Returns distinct URLs per upload so ordering of the collected list is observable.</summary>
    private void UploadsReturnSequence(params string[] urls)
    {
        var index = 0;
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => urls[index++]);
    }

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
            PasswordHash = "irrelevant-for-this-handler",
            FirstName = "Existing",
            LastName = "User",
            PhoneNumber = phoneNumber,
            RoleId = TeacherRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private static CertificateUploadRequest Certificate(string fileName = "cert-1.png") =>
        new(new MemoryStream([1, 2, 3]), fileName, "image/png", SizeInBytes: 3);

    private Task<MathInsight.Shared.Results.Result<MediatR.Unit>> RegisterAsync(
        string email = Email,
        string username = Username,
        string password = Password,
        string firstName = "New",
        string lastName = "Teacher",
        string phoneNumber = PhoneNumber,
        string? biography = "Ten years of calculus.",
        IReadOnlyList<CertificateUploadRequest>? certificates = null,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(
            new TeacherRegisterCommand(
                username, email, password, firstName, lastName, phoneNumber, biography,
                certificates ?? [Certificate()]),
            cancellationToken);

    private void VerifyNothingUploadedStoredOrSent()
    {
        _certificateStorage.Verify(
            c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task ValidRegistration_WritesNoSqlRows()
    {
        // DD-01: neither the Account nor the TeacherApplication exists until confirmation.
        await RegisterAsync();

        Assert.False(await _db.Accounts.AnyAsync(a => a.Email == Email || a.Username == Username));
        Assert.False(await _db.Teachers.AnyAsync());
        Assert.False(await _db.TeacherApplications.AnyAsync());
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
        Assert.Equal("Teacher", _capturedPayload.LastName);
        Assert.Equal(PhoneNumber, _capturedPayload.PhoneNumber);
    }

    [Fact]
    public async Task ValidRegistration_AlwaysStoresTeacherRole()
    {
        // The role is hard-coded, never taken from the request — a caller cannot self-assign one.
        CapturePayload();

        await RegisterAsync();

        Assert.Equal("Teacher", _capturedPayload!.Role);
    }

    [Fact]
    public async Task ValidRegistration_CarriesBiography()
    {
        CapturePayload();

        await RegisterAsync(biography: "Fifteen years of geometry.");

        Assert.Equal("Fifteen years of geometry.", _capturedPayload!.Biography);
    }

    [Fact]
    public async Task ValidRegistration_AcceptsNullBiography()
    {
        // Biography is optional on the request contract.
        CapturePayload();

        var result = await RegisterAsync(biography: null);

        Assert.True(result.IsSuccess);
        Assert.Null(_capturedPayload!.Biography);
    }

    [Fact]
    public async Task ValidRegistration_HashesPasswordAndNeverStoresPlaintext()
    {
        // BR-08: only a BCrypt hash may leave this handler.
        CapturePayload();

        await RegisterAsync();

        Assert.NotEqual(Password, _capturedPayload!.PasswordHash);
        Assert.StartsWith("$2", _capturedPayload.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, _capturedPayload.PasswordHash));
    }

    [Fact]
    public async Task ValidRegistration_SendsConfirmationEmailWithTheStoredToken()
    {
        await RegisterAsync();

        _emailService.Verify(
            e => e.SendRegistrationConfirmationAsync(Email, Token, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidRegistration_StoresPayloadBeforeSendingEmail()
    {
        // A mail arriving before the payload is stored would produce an unusable link.
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

    [Fact]
    public async Task ValidRegistration_UploadsCertificatesBeforeStoringThePayload()
    {
        // The payload must carry real URLs, so every upload has to complete first.
        var calls = new List<string>();
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("upload"))
            .ReturnsAsync(UploadedUrl);
        _store
            .Setup(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .ReturnsAsync(Token);

        await RegisterAsync();

        Assert.Equal(new[] { "upload", "save" }, calls);
    }

    // ---------------------------------------------------------------- BR-05 certificates

    [Fact]
    public async Task SingleCertificate_PopulatesBothUrlFields()
    {
        // DocumentsUrl feeds the legacy single-URL column; DocumentsUrls carries the full set.
        CapturePayload();
        UploadsReturnSequence(UploadedUrl);

        await RegisterAsync(certificates: [Certificate()]);

        Assert.Equal(UploadedUrl, _capturedPayload!.DocumentsUrl);
        Assert.Equal([UploadedUrl], _capturedPayload.DocumentsUrls);
    }

    [Fact]
    public async Task MultipleCertificates_AreAllUploadedInOrder()
    {
        CapturePayload();
        UploadsReturnSequence("https://cdn.test/a.png", "https://cdn.test/b.png", "https://cdn.test/c.png");

        await RegisterAsync(certificates:
            [Certificate("a.png"), Certificate("b.png"), Certificate("c.png")]);

        Assert.Equal(
            ["https://cdn.test/a.png", "https://cdn.test/b.png", "https://cdn.test/c.png"],
            _capturedPayload!.DocumentsUrls);
        _certificateStorage.Verify(
            c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task MultipleCertificates_DocumentsUrlIsTheFirstUpload()
    {
        CapturePayload();
        UploadsReturnSequence("https://cdn.test/a.png", "https://cdn.test/b.png");

        await RegisterAsync(certificates: [Certificate("a.png"), Certificate("b.png")]);

        Assert.Equal("https://cdn.test/a.png", _capturedPayload!.DocumentsUrl);
    }

    [Fact]
    public async Task NoCertificates_IsRejected()
    {
        var result = await RegisterAsync(certificates: []);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.CertificateInvalid(string.Empty).Code, result.Error!.Code);
        Assert.Contains("At least one certificate", result.Error.Message);
        VerifyNothingUploadedStoredOrSent();
    }

    [Fact]
    public async Task RejectedCertificate_ReturnsCertificateInvalidCarryingTheReason()
    {
        // BR-05: an unsupported type or oversized file is a 400 with the storage's own message.
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CertificateTooLargeException(20_000_000, 10_485_760));

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.CertificateInvalid(string.Empty).Code, result.Error!.Code);
        Assert.Contains("10 MB", result.Error.Message);
        _store.Verify(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(
            e => e.SendRegistrationConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnsupportedCertificateType_IsRejected()
    {
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnsupportedCertificateTypeException("application/x-msdownload", "malware.exe"));

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.CertificateInvalid(string.Empty).Code, result.Error!.Code);
        Assert.Contains("Unsupported certificate", result.Error.Message);
    }

    [Fact]
    public async Task RejectedCertificate_ShortCircuitsTheRemainingUploads()
    {
        // Uploads are sequential so a bad second file stops the third from ever being stored.
        var attempts = 0;
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns<CertificateUploadRequest, CancellationToken>((_, _) =>
            {
                attempts++;
                return attempts == 2
                    ? throw new UnsupportedCertificateTypeException("application/x-msdownload", "bad.exe")
                    : Task.FromResult(UploadedUrl);
            });

        var result = await RegisterAsync(certificates:
            [Certificate("a.png"), Certificate("bad.exe"), Certificate("c.png")]);

        Assert.True(result.IsFailure);
        Assert.Equal(2, attempts);   // the third file is never uploaded
    }

    [Fact]
    public async Task StorageFailure_IsNotSwallowedAsAValidationError()
    {
        // Only InvalidCertificateException maps to a 400; an infrastructure fault must surface.
        _certificateStorage
            .Setup(c => c.UploadAsync(It.IsAny<CertificateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blob storage unreachable"));

        await Assert.ThrowsAsync<HttpRequestException>(() => RegisterAsync());
    }

    // ---------------------------------------------------------------- BR-04 uniqueness

    [Fact]
    public async Task DuplicateEmail_IsRejected()
    {
        SeedAccount(email: Email, username: "someone_else");

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingUploadedStoredOrSent();
    }

    [Fact]
    public async Task DuplicateUsername_IsRejected()
    {
        SeedAccount(email: "someone.else@mathinsight.test", username: Username);

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingUploadedStoredOrSent();
    }

    [Fact]
    public async Task DuplicatePhoneNumber_IsRejectedBeforeAnyUpload()
    {
        // The phone check runs ahead of the uploads precisely so a duplicate cannot leave orphaned
        // certificate blobs behind.
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: PhoneNumber);

        var result = await RegisterAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PhoneNumberAlreadyUsed.Code, result.Error!.Code);
        VerifyNothingUploadedStoredOrSent();
    }

    [Fact]
    public async Task UnrelatedExistingAccount_DoesNotBlockRegistration()
    {
        // Negative control: a different email, username and phone must not collide.
        SeedAccount(email: "other@mathinsight.test", username: "other_user", phoneNumber: "0900000009");

        var result = await RegisterAsync();

        Assert.True(result.IsSuccess);
        _store.Verify(s => s.SaveAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingAccountsWithoutPhoneNumbers_DoNotFalselyCollide()
    {
        // Guards the phone predicate against matching the NULLs left by student accounts.
        SeedAccount(email: "other@mathinsight.test", username: "other_user", phoneNumber: null);

        var result = await RegisterAsync();

        Assert.True(result.IsSuccess);
    }

    // ---------------------------------------------------------------- input normalisation

    [Fact]
    public async Task Registration_TrimsEmailUsernamePhoneAndNames()
    {
        CapturePayload();

        await RegisterAsync(
            email: $"  {Email}  ",
            username: $"  {Username}  ",
            phoneNumber: $"  {PhoneNumber}  ",
            firstName: "  New  ",
            lastName: "  Teacher  ");

        Assert.Equal(Email, _capturedPayload!.Email);
        Assert.Equal(Username, _capturedPayload.Username);
        Assert.Equal(PhoneNumber, _capturedPayload.PhoneNumber);
        Assert.Equal("New", _capturedPayload.FirstName);
        Assert.Equal("Teacher", _capturedPayload.LastName);
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
        SeedAccount(email: Email, username: "someone_else");

        var result = await RegisterAsync(email: $"   {Email}   ");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.EmailAlreadyConfirmed.Code, result.Error!.Code);
        VerifyNothingUploadedStoredOrSent();
    }

    [Fact]
    public async Task PaddedPhoneNumber_StillDetectedAsDuplicate()
    {
        SeedAccount(email: "someone.else@mathinsight.test", username: "someone_else", phoneNumber: PhoneNumber);

        var result = await RegisterAsync(phoneNumber: $"   {PhoneNumber}   ");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PhoneNumberAlreadyUsed.Code, result.Error!.Code);
        VerifyNothingUploadedStoredOrSent();
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

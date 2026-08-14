using MathInsight.Modules.Identity_Access.Commands.UpdateProfile;
using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Queries.GetProfile;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

/// <summary>
/// Unit tests for UpdateProfileCommandHandler (UC-04/UC-05).
///
/// The rule that shapes almost every test is <c>?? existing</c>: this is a PARTIAL update, so a
/// null field must leave the stored value alone. The handler delegates its response to
/// GetProfileQuery through IMediator, so the mediator is a Moq double returning a canned
/// projection — the assertions are made against the persisted rows, which is where the behaviour
/// under test actually lives. EF Core InMemory stands in for SQL Server.
/// </summary>
public class UpdateProfileCommandHandlerTests : IDisposable
{
    private const string StudentRoleId = "44444444-4444-4444-4444-444444444444";
    private const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";
    private const string ExpertRoleId = "22222222-2222-2222-2222-222222222222";

    private const string AccountId = "acct-A";
    private const string Email = "user-a@mathinsight.test";
    private const string Username = "user_a";

    private static readonly DateOnly StoredDateOfBirth = new(2008, 5, 20);

    private readonly IdentityDbContext _db;
    private readonly Mock<IMediator> _mediator = new();
    private readonly UpdateProfileCommandHandler _handler;

    public UpdateProfileCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        EnsureRole(StudentRoleId, "Student");
        EnsureRole(TeacherRoleId, "Teacher");
        EnsureRole(ExpertRoleId, "Expert");

        _mediator
            .Setup(m => m.Send(It.IsAny<GetProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProfileResponse>.Success(CannedProfile));

        _handler = new UpdateProfileCommandHandler(_db, _mediator.Object);
    }

    public void Dispose() => _db.Dispose();

    private static ProfileResponse CannedProfile => new(
        Username, Email, "Projected", "Profile", null, null, null, "Student", null, null, null);

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

    private void SeedAccount(
        string roleId = StudentRoleId,
        bool isActive = true,
        string firstName = "Original",
        string lastName = "Name",
        string? phoneNumber = "0900000001",
        DateOnly? dateOfBirth = null)
    {
        _db.Accounts.Add(new Account
        {
            AccountId = AccountId,
            Username = Username,
            Email = Email,
            PasswordHash = "$2a$12$abcdefghijklmnopqrstuvwxyz012345678901234567890123456",
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            DateOfBirth = dateOfBirth ?? StoredDateOfBirth,
            RoleId = roleId,
            IsActive = isActive,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void SeedStudentRow(string? gender = "Female", string? school = "Le Quy Don", int? currentGrade = 11)
    {
        _db.Students.Add(new Student
        {
            StudentId = AccountId,
            Gender = gender,
            School = school,
            CurrentGrade = currentGrade
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void SeedTeacherRow(string? biography = "Original biography.", bool isVerified = true)
    {
        _db.Teachers.Add(new Teacher
        {
            TeacherId = AccountId,
            Biography = biography,
            IsVerified = isVerified
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void SeedExpertRow(string? specialty = "Algebra")
    {
        _db.Experts.Add(new Expert { ExpertId = AccountId, Specialty = specialty });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Task<Result<ProfileResponse>> UpdateAsync(
        string accountId = AccountId,
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        DateOnly? dateOfBirth = null,
        string? gender = null,
        string? school = null,
        int? currentGrade = null,
        string? biography = null,
        string? specialty = null,
        CancellationToken cancellationToken = default) =>
        _handler.Handle(
            new UpdateProfileCommand(
                accountId, firstName, lastName, phoneNumber, dateOfBirth,
                gender, school, currentGrade, biography, specialty),
            cancellationToken);

    private Task<Account> LoadAccountAsync() =>
        _db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == AccountId);

    private Task<Student> LoadStudentAsync() =>
        _db.Students.AsNoTracking().SingleAsync(s => s.StudentId == AccountId);

    private Task<Teacher> LoadTeacherAsync() =>
        _db.Teachers.AsNoTracking().SingleAsync(t => t.TeacherId == AccountId);

    private Task<Expert> LoadExpertAsync() =>
        _db.Experts.AsNoTracking().SingleAsync(e => e.ExpertId == AccountId);

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ValidUpdate_PersistsTheSuppliedAccountFields()
    {
        SeedAccount();
        SeedStudentRow();

        var result = await UpdateAsync(
            firstName: "Ada",
            lastName: "Lovelace",
            phoneNumber: "0911111111",
            dateOfBirth: new DateOnly(2009, 1, 2));

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync();
        Assert.Equal("Ada", account.FirstName);
        Assert.Equal("Lovelace", account.LastName);
        Assert.Equal("0911111111", account.PhoneNumber);
        Assert.Equal(new DateOnly(2009, 1, 2), account.DateOfBirth);
    }

    [Fact]
    public async Task ValidUpdate_ReturnsTheGetProfileProjection()
    {
        // Response shape is delegated so GET and PUT can never drift apart.
        SeedAccount();
        SeedStudentRow();

        var result = await UpdateAsync(firstName: "Ada");

        Assert.True(result.IsSuccess);
        Assert.Equal("Projected", result.Value!.FirstName);
        _mediator.Verify(
            m => m.Send(It.Is<GetProfileQuery>(q => q.AccountId == AccountId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------- partial update semantics

    [Fact]
    public async Task NullFields_LeaveStoredAccountValuesIntact()
    {
        // The core of UC-05: an omitted field is not a request to clear the value.
        SeedAccount(firstName: "Original", lastName: "Name", phoneNumber: "0900000001");
        SeedStudentRow();

        await UpdateAsync(firstName: "Ada");   // everything else null

        var account = await LoadAccountAsync();
        Assert.Equal("Ada", account.FirstName);
        Assert.Equal("Name", account.LastName);              // untouched
        Assert.Equal("0900000001", account.PhoneNumber);     // untouched
        Assert.Equal(StoredDateOfBirth, account.DateOfBirth); // untouched
    }

    [Fact]
    public async Task AllNullFields_ChangeNothing()
    {
        SeedAccount();
        SeedStudentRow();

        var result = await UpdateAsync();   // a completely empty patch

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync();
        Assert.Equal("Original", account.FirstName);
        Assert.Equal("Name", account.LastName);
        Assert.Equal("0900000001", account.PhoneNumber);
        var student = await LoadStudentAsync();
        Assert.Equal("Female", student.Gender);
        Assert.Equal("Le Quy Don", student.School);
        Assert.Equal(11, student.CurrentGrade);
    }

    [Fact]
    public async Task NullFields_LeaveStoredStudentValuesIntact()
    {
        SeedAccount();
        SeedStudentRow(gender: "Female", school: "Le Quy Don", currentGrade: 11);

        await UpdateAsync(school: "Chu Van An");   // gender and grade omitted

        var student = await LoadStudentAsync();
        Assert.Equal("Chu Van An", student.School);
        Assert.Equal("Female", student.Gender);   // untouched
        Assert.Equal(11, student.CurrentGrade);   // untouched
    }

    [Fact]
    public async Task EmptyString_OverwritesTheStoredValue()
    {
        // Documents the boundary: only null is treated as "leave alone". An empty string IS a
        // value and clears the stored one — the guard is `?? existing`, not a blank check.
        SeedAccount(firstName: "Original");
        SeedStudentRow();

        await UpdateAsync(firstName: string.Empty);

        var account = await LoadAccountAsync();
        Assert.Equal(string.Empty, account.FirstName);
    }

    [Fact]
    public async Task WhitespaceValues_ArePersistedWithoutTrimming()
    {
        // This handler performs no Trim(); padding is stored verbatim.
        SeedAccount();
        SeedStudentRow();

        await UpdateAsync(firstName: "  Ada  ");

        var account = await LoadAccountAsync();
        Assert.Equal("  Ada  ", account.FirstName);
    }

    // ---------------------------------------------------------------- role-specific fields

    [Fact]
    public async Task StudentFields_AreWrittenForAStudent()
    {
        SeedAccount(roleId: StudentRoleId);
        SeedStudentRow();

        await UpdateAsync(gender: "Male", school: "Chu Van An", currentGrade: 12);

        var student = await LoadStudentAsync();
        Assert.Equal("Male", student.Gender);
        Assert.Equal("Chu Van An", student.School);
        Assert.Equal(12, student.CurrentGrade);
    }

    [Fact]
    public async Task TeacherBiography_IsWrittenForATeacher()
    {
        SeedAccount(roleId: TeacherRoleId);
        SeedTeacherRow(biography: "Original biography.");

        await UpdateAsync(biography: "Fifteen years of geometry.");

        var teacher = await LoadTeacherAsync();
        Assert.Equal("Fifteen years of geometry.", teacher.Biography);
    }

    [Fact]
    public async Task ExpertSpecialty_IsWrittenForAnExpert()
    {
        SeedAccount(roleId: ExpertRoleId);
        SeedExpertRow(specialty: "Algebra");

        await UpdateAsync(specialty: "Number Theory");

        var expert = await LoadExpertAsync();
        Assert.Equal("Number Theory", expert.Specialty);
    }

    [Fact]
    public async Task StudentSendingTeacherAndExpertFields_IsIgnoredNotRejected()
    {
        // There is no Teacher or Expert row to write to, so the fields are silently dropped.
        SeedAccount(roleId: StudentRoleId);
        SeedStudentRow();

        var result = await UpdateAsync(
            school: "Chu Van An", biography: "I am secretly a teacher", specialty: "Everything");

        Assert.True(result.IsSuccess);
        Assert.Equal("Chu Van An", (await LoadStudentAsync()).School);
        Assert.False(await _db.Teachers.AnyAsync());
        Assert.False(await _db.Experts.AnyAsync());
    }

    [Fact]
    public async Task TeacherSendingStudentFields_LeavesTheTeacherRowAlone()
    {
        SeedAccount(roleId: TeacherRoleId);
        SeedTeacherRow(biography: "Original biography.");

        var result = await UpdateAsync(gender: "Male", school: "Chu Van An", currentGrade: 12);

        Assert.True(result.IsSuccess);
        Assert.Equal("Original biography.", (await LoadTeacherAsync()).Biography);
        Assert.False(await _db.Students.AnyAsync());
    }

    [Fact]
    public async Task TeacherVerificationStatus_IsNeverTouched()
    {
        // UC-15: IsVerified is Admin-controlled and is not part of this command at all.
        SeedAccount(roleId: TeacherRoleId);
        SeedTeacherRow(biography: "Original biography.", isVerified: true);

        await UpdateAsync(biography: "Rewritten.");

        var teacher = await LoadTeacherAsync();
        Assert.True(teacher.IsVerified);
    }

    [Fact]
    public async Task AccountWithNoRoleSpecificRow_UpdatesOnlyTheAccountFields()
    {
        // e.g. an Admin: none of the three navigations is populated.
        SeedAccount(roleId: StudentRoleId);   // no Student row seeded

        var result = await UpdateAsync(firstName: "Ada", gender: "Male", biography: "x", specialty: "y");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", (await LoadAccountAsync()).FirstName);
        Assert.False(await _db.Students.AnyAsync());
    }

    // ---------------------------------------------------------------- immutable fields

    [Fact]
    public async Task EmailUsernameAndRole_CannotBeChangedHere()
    {
        // They are absent from the command by design; this pins that a full update leaves them be.
        SeedAccount(roleId: StudentRoleId);
        SeedStudentRow();

        await UpdateAsync(
            firstName: "Ada", lastName: "Lovelace", phoneNumber: "0911111111",
            dateOfBirth: new DateOnly(2009, 1, 2), gender: "Male", school: "Chu Van An", currentGrade: 12);

        var account = await LoadAccountAsync();
        Assert.Equal(Email, account.Email);
        Assert.Equal(Username, account.Username);
        Assert.Equal(StudentRoleId, account.RoleId);
    }

    [Fact]
    public async Task PasswordHashAndActiveFlag_AreUntouched()
    {
        SeedAccount();
        SeedStudentRow();
        var originalHash = (await LoadAccountAsync()).PasswordHash;

        await UpdateAsync(firstName: "Ada");

        var account = await LoadAccountAsync();
        Assert.Equal(originalHash, account.PasswordHash);
        Assert.True(account.IsActive);
    }

    // ---------------------------------------------------------------- account resolution

    [Fact]
    public async Task UnknownAccountId_ReturnsTokenInvalid()
    {
        var result = await UpdateAsync(accountId: "deleted-account", firstName: "Ada");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.TokenInvalid.Code, result.Error!.Code);
        _mediator.Verify(m => m.Send(It.IsAny<GetProfileQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTargetsOnlyTheCallersOwnAccount()
    {
        // The account id comes from the caller's token; a second account must be unaffected.
        SeedAccount();
        SeedStudentRow();
        _db.Accounts.Add(new Account
        {
            AccountId = "acct-B",
            Username = "user_b",
            Email = "user-b@mathinsight.test",
            PasswordHash = "$2a$12$abcdefghijklmnopqrstuvwxyz012345678901234567890123456",
            FirstName = "Other",
            LastName = "Person",
            RoleId = StudentRoleId,
            IsActive = true,
            CreatedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await UpdateAsync(firstName: "Ada");

        var other = await _db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == "acct-B");
        Assert.Equal("Other", other.FirstName);
    }

    [Fact]
    public async Task DeactivatedAccount_IsStillUpdated()
    {
        // Documents current behaviour: this handler does NOT check IsActive, so a deactivated
        // account holding a still-valid access token can edit its own profile.
        SeedAccount(isActive: false);
        SeedStudentRow();

        var result = await UpdateAsync(firstName: "Ada");

        Assert.True(result.IsSuccess);
        var account = await LoadAccountAsync();
        Assert.Equal("Ada", account.FirstName);
        Assert.False(account.IsActive);   // deactivation itself is unchanged
    }

    // ---------------------------------------------------------------- cancellation

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        SeedAccount();
        SeedStudentRow();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => UpdateAsync(firstName: "Ada", cancellationToken: cts.Token));
    }
}

using MathInsight.Modules.Identity_Access.Commands.UpdateAccount;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class UpdateAccountCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly UpdateAccountCommandHandler _handler;

    public UpdateAccountCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _handler = new UpdateAccountCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Role> SeedRoleAsync(string name)
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = name };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task<Account> SeedAccountAsync(string roleId, string email, string username = "target")
    {
        var account = new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            PasswordHash = "hash",
            FirstName = "Old",
            LastName = "Name",
            RoleId = roleId,
            IsActive = true,
            CreatedTime = DateTime.UtcNow
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task Handle_AccountNotFound_ReturnsFailure()
    {
        var role = await SeedRoleAsync("Student");
        var command = new UpdateAccountCommand("missing-id", "A", "B", "a@x.com", role.RoleId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AccountNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_NewEmailTakenByAnotherAccount_ReturnsFailure()
    {
        var role = await SeedRoleAsync("Student");
        var target = await SeedAccountAsync(role.RoleId, "old@x.com");
        await SeedAccountAsync(role.RoleId, "new@x.com", username: "other");

        var command = new UpdateAccountCommand(target.AccountId, "A", "B", "new@x.com", role.RoleId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.EmailAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Handle_EmailUnchangedCaseInsensitive_SkipsUniquenessCheck()
    {
        var role = await SeedRoleAsync("Student");
        var target = await SeedAccountAsync(role.RoleId, "User@X.com");
        // Another unrelated account already holds the same email address (case-different).
        // If the uniqueness check ran, this would incorrectly fail the update.
        await SeedAccountAsync(role.RoleId, "user@x.com", username: "other");

        var command = new UpdateAccountCommand(target.AccountId, "New", "Name", "user@x.com", role.RoleId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_TargetRoleNotFound_ReturnsFailure()
    {
        var role = await SeedRoleAsync("Student");
        var target = await SeedAccountAsync(role.RoleId, "a@x.com");

        var command = new UpdateAccountCommand(target.AccountId, "A", "B", "a@x.com", "missing-role-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RoleNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_RoleChangeStudentToTeacher_SwapsRoleSpecificRows()
    {
        var studentRole = await SeedRoleAsync("Student");
        var teacherRole = await SeedRoleAsync("Teacher");
        var target = await SeedAccountAsync(studentRole.RoleId, "a@x.com");
        _db.Students.Add(new Student { StudentId = target.AccountId });
        await _db.SaveChangesAsync();

        var command = new UpdateAccountCommand(target.AccountId, "A", "B", "a@x.com", teacherRole.RoleId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await _db.Students.FindAsync(target.AccountId));
        var teacher = await _db.Teachers.FindAsync(target.AccountId);
        Assert.NotNull(teacher);
        Assert.True(teacher!.IsVerified);
        var updated = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == target.AccountId);
        Assert.Equal(teacherRole.RoleId, updated.RoleId);
    }

    [Fact]
    public async Task Handle_RoleUnchanged_LeavesRoleSpecificRowsUntouched()
    {
        var role = await SeedRoleAsync("Student");
        var target = await SeedAccountAsync(role.RoleId, "a@x.com");
        _db.Students.Add(new Student { StudentId = target.AccountId, School = "Original School" });
        await _db.SaveChangesAsync();

        var command = new UpdateAccountCommand(target.AccountId, "New", "Name", "a@x.com", role.RoleId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var student = await _db.Students.FindAsync(target.AccountId);
        Assert.NotNull(student);
        Assert.Equal("Original School", student!.School);
    }

    [Fact]
    public async Task Handle_UpdatesNamesAndEmail_RoleUnchanged_HappyPath()
    {
        var role = await SeedRoleAsync("Student");
        var target = await SeedAccountAsync(role.RoleId, "old@x.com");

        var command = new UpdateAccountCommand(target.AccountId, "NewFirst", "NewLast", "new@x.com", role.RoleId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NewFirst", result.Value!.FirstName);
        Assert.Equal("NewLast", result.Value.LastName);
        Assert.Equal("new@x.com", result.Value.Email);
    }
}

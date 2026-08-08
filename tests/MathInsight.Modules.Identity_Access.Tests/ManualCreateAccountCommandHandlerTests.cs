using MathInsight.Modules.Identity_Access.Commands.ManualCreateAccount;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class ManualCreateAccountCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IMediator> _mediator = new();
    private readonly ManualCreateAccountCommandHandler _handler;

    public ManualCreateAccountCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _mediator.Setup(m => m.Publish(It.IsAny<AccountCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ManualCreateAccountCommandHandler(_db, _mediator.Object);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Role> SeedRoleAsync(string name)
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = name };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private static ManualCreateAccountCommand ValidCommand(string roleName, string? username = null, string? email = null) =>
        new(
            username ?? $"user-{Guid.NewGuid():N}",
            email ?? $"{Guid.NewGuid():N}@example.com",
            "Password1!",
            "An",
            "Nguyen",
            null,
            null,
            roleName);

    [Fact]
    public async Task Handle_PasswordShorterThan8Chars_ReturnsFailure()
    {
        var command = ValidCommand("Student") with { Password = "Pw1!" };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.PasswordTooShort, result.Error);
        Assert.Equal(0, await _db.Accounts.CountAsync());
    }

    [Fact]
    public async Task Handle_RoleNameNotFound_ReturnsFailure()
    {
        var command = ValidCommand("Guardian");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidRole, result.Error);
    }

    [Fact]
    public async Task Handle_RoleExistsButNotStudentTeacherExpert_ReturnsFailure()
    {
        await SeedRoleAsync("Admin");
        var command = ValidCommand("Admin");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidRole, result.Error);
    }

    [Fact]
    public async Task Handle_UsernameAlreadyExists_ReturnsFailure()
    {
        var role = await SeedRoleAsync("Student");
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = "dupuser",
            Email = "existing@example.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            RoleId = role.RoleId,
            IsActive = true,
            CreatedTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var command = ValidCommand("Student", username: "dupuser");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.UsernameAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsFailure()
    {
        var role = await SeedRoleAsync("Student");
        _db.Accounts.Add(new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = "existinguser",
            Email = "taken@example.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            RoleId = role.RoleId,
            IsActive = true,
            CreatedTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var command = ValidCommand("Student", email: "taken@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.EmailAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Handle_CreatesStudentAccount_AddsStudentRow_PublishesEvent()
    {
        var role = await SeedRoleAsync("Student");
        var command = ValidCommand("Student", username: "newstudent");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var accountId = result.Value!.AccountId;
        Assert.NotNull(await _db.Students.FindAsync(accountId));
        var account = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == accountId);
        Assert.True(account.IsActive);
        Assert.Equal(role.RoleId, account.RoleId);
        _mediator.Verify(m => m.Publish(
            It.Is<AccountCreatedEvent>(e =>
                e.AccountId == accountId && e.Username == "newstudent" && e.RoleName == "Student"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesTeacherAccount_AutoVerifiedAndActive()
    {
        await SeedRoleAsync("Teacher");
        var command = ValidCommand("Teacher", username: "newteacher");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var accountId = result.Value!.AccountId;
        var teacher = await _db.Teachers.FindAsync(accountId);
        Assert.NotNull(teacher);
        Assert.True(teacher!.IsVerified);
        var account = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == accountId);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task Handle_CreatesExpertAccount()
    {
        await SeedRoleAsync("Expert");
        var command = ValidCommand("Expert", username: "newexpert");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(await _db.Experts.FindAsync(result.Value!.AccountId));
    }
}

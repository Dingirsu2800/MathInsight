using MathInsight.Modules.Identity_Access.Commands.ToggleAccountStatus;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class ToggleAccountStatusCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly ToggleAccountStatusCommandHandler _handler;

    public ToggleAccountStatusCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _handler = new ToggleAccountStatusCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Account> SeedAccountAsync(bool isActive)
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Admin" };
        _db.Roles.Add(role);
        var account = new Account
        {
            AccountId = Guid.NewGuid().ToString(),
            Username = "target",
            Email = "target@x.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            RoleId = role.RoleId,
            IsActive = isActive,
            CreatedTime = DateTime.UtcNow
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task Handle_AdminDeactivatesOwnAccount_ReturnsFailure()
    {
        var admin = await SeedAccountAsync(isActive: true);
        var command = new ToggleAccountStatusCommand(admin.AccountId, false, admin.AccountId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotDeactivateSelf, result.Error);
        var unchanged = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == admin.AccountId);
        Assert.True(unchanged.IsActive);
    }

    [Fact]
    public async Task Handle_AdminReactivatesOwnAccount_IsAllowed()
    {
        var admin = await SeedAccountAsync(isActive: false);
        var command = new ToggleAccountStatusCommand(admin.AccountId, true, admin.AccountId);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
    }

    [Fact]
    public async Task Handle_TargetAccountNotFound_ReturnsFailure()
    {
        var command = new ToggleAccountStatusCommand("missing-id", false, "requester-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AccountNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_DeactivateAnotherAccount_HappyPath()
    {
        var target = await SeedAccountAsync(isActive: true);
        var command = new ToggleAccountStatusCommand(target.AccountId, false, "some-other-admin-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        var updated = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == target.AccountId);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Handle_ReactivateAnotherAccount_HappyPath()
    {
        var target = await SeedAccountAsync(isActive: false);
        var command = new ToggleAccountStatusCommand(target.AccountId, true, "some-other-admin-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
    }
}

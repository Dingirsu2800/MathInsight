using MathInsight.Modules.Identity_Access.Commands.AdjustPermission;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class AdjustPermissionCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly AdjustPermissionCommandHandler _handler;

    public AdjustPermissionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _handler = new AdjustPermissionCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Permission> SeedPermissionAsync(string key)
    {
        var permission = new Permission { PermissionId = Guid.NewGuid().ToString(), PermissionKey = key };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
        return permission;
    }

    private async Task<(Role role, Account admin, Permission adminAccess)> SeedAdminRoleWithRequesterAsync()
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Admin" };
        var adminAccess = new Permission { PermissionId = Guid.NewGuid().ToString(), PermissionKey = IdentityPermissionKeys.AdminAccess };
        var admin = new Account
        {
            AccountId = Guid.NewGuid().ToString(), Username = "admin1", Email = "admin1@x.com",
            PasswordHash = "hash", FirstName = "A", LastName = "B", RoleId = role.RoleId,
            IsActive = true, CreatedTime = DateTime.UtcNow
        };
        _db.Roles.Add(role);
        _db.Permissions.Add(adminAccess);
        _db.Accounts.Add(admin);
        _db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = adminAccess.PermissionId });
        await _db.SaveChangesAsync();
        return (role, admin, adminAccess);
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsFailure()
    {
        var command = new AdjustPermissionCommand("missing-id", Array.Empty<string>(), "requester-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RoleNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_RequestedPermissionDoesNotExist_ReturnsFailure()
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        _db.Roles.Add(role);
        var p1 = await SeedPermissionAsync("auth:login");
        await _db.SaveChangesAsync();

        var command = new AdjustPermissionCommand(role.RoleId, new[] { p1.PermissionId, "missing-permission-id" }, "requester-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.PermissionNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_AdminRemovesOwnAdminAccessPermission_ReturnsFailure()
    {
        var (role, admin, adminAccess) = await SeedAdminRoleWithRequesterAsync();
        var otherPermission = await SeedPermissionAsync("account:import");

        // Requested set excludes AdminAccess entirely.
        var command = new AdjustPermissionCommand(role.RoleId, new[] { otherPermission.PermissionId }, admin.AccountId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.CannotRemoveOwnAdminPermission, result.Error);
        Assert.Equal(1, await _db.RolePermissions.CountAsync(rp => rp.RoleId == role.RoleId));
    }

    [Fact]
    public async Task Handle_AdminKeepsAdminAccessPermission_Succeeds()
    {
        var (role, admin, adminAccess) = await SeedAdminRoleWithRequesterAsync();
        var otherPermission = await SeedPermissionAsync("account:import");

        var command = new AdjustPermissionCommand(role.RoleId, new[] { adminAccess.PermissionId, otherPermission.PermissionId }, admin.AccountId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await _db.RolePermissions.CountAsync(rp => rp.RoleId == role.RoleId));
    }

    [Fact]
    public async Task Handle_AdminEditsDifferentRole_SelfGuardIsSkipped()
    {
        var (adminRole, admin, _) = await SeedAdminRoleWithRequesterAsync();
        var teacherRole = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        _db.Roles.Add(teacherRole);
        await _db.SaveChangesAsync();

        // Empty permission set on a role that is NOT the requester's own role.
        var command = new AdjustPermissionCommand(teacherRole.RoleId, Array.Empty<string>(), admin.AccountId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Permissions, p => Assert.False(p.IsGranted));
    }

    [Fact]
    public async Task Handle_DuplicatePermissionIdsInRequest_AreDeduplicated()
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        _db.Roles.Add(role);
        var p1 = await SeedPermissionAsync("auth:login");
        var p2 = await SeedPermissionAsync("account:register");
        await _db.SaveChangesAsync();

        var command = new AdjustPermissionCommand(role.RoleId, new[] { p1.PermissionId, p1.PermissionId, p2.PermissionId }, "requester-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await _db.RolePermissions.CountAsync(rp => rp.RoleId == role.RoleId));
    }

    [Fact]
    public async Task Handle_FullPermissionSetReplacement_HappyPath()
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        var oldP1 = await SeedPermissionAsync("auth:login");
        var oldP2 = await SeedPermissionAsync("account:register");
        _db.Roles.Add(role);
        _db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = oldP1.PermissionId });
        _db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = oldP2.PermissionId });
        await _db.SaveChangesAsync();

        var newP1 = await SeedPermissionAsync("account:import");
        var newP2 = await SeedPermissionAsync("permission:adjust");

        var command = new AdjustPermissionCommand(role.RoleId, new[] { newP1.PermissionId, newP2.PermissionId }, "requester-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var grantedKeys = result.Value!.Permissions.Where(p => p.IsGranted).Select(p => p.PermissionKey).ToHashSet();
        Assert.Equal(new HashSet<string> { "account:import", "permission:adjust" }, grantedKeys);
        Assert.Equal(2, await _db.RolePermissions.CountAsync(rp => rp.RoleId == role.RoleId));
    }
}

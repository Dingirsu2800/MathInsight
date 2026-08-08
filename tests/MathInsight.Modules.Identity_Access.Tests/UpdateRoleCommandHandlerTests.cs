using MathInsight.Modules.Identity_Access.Commands.UpdateRole;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class UpdateRoleCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly UpdateRoleCommandHandler _handler;

    public UpdateRoleCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _handler = new UpdateRoleCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Role> SeedRoleAsync(string name, string? description = null)
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = name, Description = description };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsFailure()
    {
        var command = new UpdateRoleCommand("missing-id", "NewName", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RoleNotFound, result.Error);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Expert")]
    [InlineData("Teacher")]
    [InlineData("Student")]
    public async Task Handle_RenamingSystemRole_ReturnsFailure(string systemRoleName)
    {
        var role = await SeedRoleAsync(systemRoleName);
        var command = new UpdateRoleCommand(role.RoleId, "Educator", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.SystemRoleRenameForbidden, result.Error);
        var unchanged = await _db.Roles.AsNoTracking().FirstAsync(r => r.RoleId == role.RoleId);
        Assert.Equal(systemRoleName, unchanged.RoleName);
    }

    [Fact]
    public async Task Handle_RenameToNameAlreadyUsedByAnotherRole_ReturnsFailure()
    {
        var grader = await SeedRoleAsync("Grader");
        await SeedRoleAsync("Reviewer");

        var command = new UpdateRoleCommand(grader.RoleId, "Reviewer", null);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RoleNameAlreadyExists, result.Error);
    }

    [Fact]
    public async Task Handle_RenameCustomRoleToUniqueName_HappyPath()
    {
        var grader = await SeedRoleAsync("Grader");

        var command = new UpdateRoleCommand(grader.RoleId, "SeniorGrader", null);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SeniorGrader", result.Value!.RoleName);
        var updated = await _db.Roles.AsNoTracking().FirstAsync(r => r.RoleId == grader.RoleId);
        Assert.Equal("SeniorGrader", updated.RoleName);
    }

    [Fact]
    public async Task Handle_RoleNameSameAsCurrentCaseInsensitive_IsNotTreatedAsRename()
    {
        var grader = await SeedRoleAsync("Grader");
        // A different role also named "Reviewer" would trigger RoleNameAlreadyExists if the
        // uniqueness check ran — it must not, because this isn't actually a rename.
        await SeedRoleAsync("Reviewer");

        var command = new UpdateRoleCommand(grader.RoleId, "GRADER", "Updated description");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Grader", result.Value!.RoleName);
        Assert.Equal("Updated description", result.Value.Description);
    }

    [Fact]
    public async Task Handle_DescriptionOnlyUpdate_RoleNameUnchanged()
    {
        var grader = await SeedRoleAsync("Grader", "old description");

        var command = new UpdateRoleCommand(grader.RoleId, null, "new description");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Grader", result.Value!.RoleName);
        Assert.Equal("new description", result.Value.Description);
    }
}

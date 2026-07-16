using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateRole;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result<RoleResponse>>
{
    private static readonly HashSet<string> SystemRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Expert", "Teacher", "Student"
    };

    private readonly IdentityDbContext _dbContext;

    public UpdateRoleCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<RoleResponse>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(role => role.RoleId == request.RoleId, cancellationToken);

        if (role is null)
            return Result<RoleResponse>.Failure(IdentityErrors.RoleNotFound);

        var wantsRename = !string.IsNullOrWhiteSpace(request.RoleName) &&
            !string.Equals(request.RoleName, role.RoleName, StringComparison.OrdinalIgnoreCase);

        if (wantsRename)
        {
            if (SystemRoleNames.Contains(role.RoleName))
                return Result<RoleResponse>.Failure(IdentityErrors.SystemRoleRenameForbidden);

            var nameTaken = await _dbContext.Roles
                .AnyAsync(other => other.RoleId != role.RoleId && other.RoleName == request.RoleName, cancellationToken);
            if (nameTaken)
                return Result<RoleResponse>.Failure(IdentityErrors.RoleNameAlreadyExists);

            role.RoleName = request.RoleName!;
        }

        if (request.Description is not null)
            role.Description = request.Description;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<RoleResponse>.Success(new RoleResponse(
            role.RoleId,
            role.RoleName,
            role.Description,
            role.RolePermissions
                .Select(rolePermission => new PermissionResponse(
                    rolePermission.Permission.PermissionId,
                    rolePermission.Permission.PermissionKey,
                    rolePermission.Permission.Description,
                    true))
                .ToList()));
    }
}

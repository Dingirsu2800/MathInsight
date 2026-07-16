using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.AdjustPermission;

public class AdjustPermissionCommandHandler
    : IRequestHandler<AdjustPermissionCommand, Result<RoleResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public AdjustPermissionCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<RoleResponse>> Handle(AdjustPermissionCommand request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .Include(role => role.RolePermissions)
            .FirstOrDefaultAsync(role => role.RoleId == request.RoleId, cancellationToken);

        if (role is null)
            return Result<RoleResponse>.Failure(IdentityErrors.RoleNotFound);

        var requestedPermissionIds = request.PermissionIds.Distinct().ToList();

        var matchedPermissions = await _dbContext.Permissions
            .Where(permission => requestedPermissionIds.Contains(permission.PermissionId))
            .ToListAsync(cancellationToken);

        if (matchedPermissions.Count != requestedPermissionIds.Count)
            return Result<RoleResponse>.Failure(IdentityErrors.PermissionNotFound);

        var requestingAccount = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.AccountId == request.RequestingAccountId, cancellationToken);

        if (requestingAccount is not null &&
            string.Equals(role.RoleId, requestingAccount.RoleId, StringComparison.Ordinal) &&
            string.Equals(role.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var stillHasAdminAccess = matchedPermissions
                .Any(permission => permission.PermissionKey == IdentityPermissionKeys.AdminAccess);

            if (!stillHasAdminAccess)
                return Result<RoleResponse>.Failure(IdentityErrors.CannotRemoveOwnAdminPermission);
        }

        _dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permission in matchedPermissions)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permission.PermissionId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var allPermissions = await _dbContext.Permissions.ToListAsync(cancellationToken);
        var grantedIds = matchedPermissions.Select(permission => permission.PermissionId).ToHashSet();

        return Result<RoleResponse>.Success(new RoleResponse(
            role.RoleId,
            role.RoleName,
            role.Description,
            allPermissions
                .Select(permission => new PermissionResponse(
                    permission.PermissionId,
                    permission.PermissionKey,
                    permission.Description,
                    grantedIds.Contains(permission.PermissionId)))
                .ToList()));
    }
}

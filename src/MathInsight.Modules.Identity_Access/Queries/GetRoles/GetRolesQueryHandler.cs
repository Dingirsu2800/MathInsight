using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetRoles;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleResponse>>>
{
    private readonly IdentityDbContext _dbContext;

    public GetRolesQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var allPermissions = await _dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
            .ToListAsync(cancellationToken);

        var response = roles
            .Select(role =>
            {
                var grantedIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
                return new RoleResponse(
                    role.RoleId,
                    role.RoleName,
                    role.Description,
                    allPermissions
                        .Select(permission => new PermissionResponse(
                            permission.PermissionId,
                            permission.PermissionKey,
                            permission.Description,
                            grantedIds.Contains(permission.PermissionId)))
                        .ToList());
            })
            .ToList();

        return Result<IReadOnlyList<RoleResponse>>.Success(response);
    }
}

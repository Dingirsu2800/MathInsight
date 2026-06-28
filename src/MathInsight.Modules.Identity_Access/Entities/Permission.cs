using System;
using System.Collections.Generic;
using System.Text;

namespace MathInsight.Modules.Identity_Access.Entities;

public class Permission
{
    public string PermissionId { get; set; } = default!;
    public string PermissionKey { get; set; } = default!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}


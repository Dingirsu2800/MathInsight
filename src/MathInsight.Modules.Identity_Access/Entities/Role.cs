namespace MathInsight.Modules.Identity_Access.Entities;

public class Role
{
    public string RoleId { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
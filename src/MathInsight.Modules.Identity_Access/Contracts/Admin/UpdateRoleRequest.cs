using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed class UpdateRoleRequest
{
    [MaxLength(50)]
    public string? RoleName { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }
}

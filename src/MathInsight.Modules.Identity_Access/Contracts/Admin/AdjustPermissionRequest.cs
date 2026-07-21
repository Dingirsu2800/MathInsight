using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed class AdjustPermissionRequest
{
    /// <summary>The full desired set of permission ids granted to the role.</summary>
    [Required]
    public List<string> PermissionIds { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed class UpdateAccountRequest
{
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = default!;

    [Required, MaxLength(100)]
    public string Email { get; set; } = default!;

    [Required]
    public string RoleId { get; set; } = default!;
}

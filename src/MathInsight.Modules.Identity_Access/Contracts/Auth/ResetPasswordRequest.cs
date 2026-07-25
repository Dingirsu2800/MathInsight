using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

// UC-06 part 1. The response is intentionally identical whether or not this email is
// registered (enumeration protection), so no other fields are needed.
public class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = default!;
}

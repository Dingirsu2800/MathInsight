using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class LoginRequest
{
    [Required]
    public string UsernameOrEmail { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}

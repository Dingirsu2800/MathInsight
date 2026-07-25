using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = default!;
}

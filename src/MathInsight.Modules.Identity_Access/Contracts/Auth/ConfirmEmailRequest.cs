using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class ConfirmEmailRequest
{
    [Required]
    public string Token { get; set; } = default!;
}

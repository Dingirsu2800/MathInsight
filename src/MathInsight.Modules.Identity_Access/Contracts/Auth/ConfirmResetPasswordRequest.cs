using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

// UC-06 part 2. BR-08 is enforced here via the same attributes as StudentRegisterRequest, so
// an invalid password fails model validation with HTTP 400 before the handler runs.
public class ConfirmResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    [RegularExpression(AuthValidation.PasswordPattern, ErrorMessage = AuthValidation.PasswordMessage)]
    public string NewPassword { get; set; } = default!;
}

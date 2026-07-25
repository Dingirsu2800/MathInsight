using System.ComponentModel.DataAnnotations;
using MathInsight.Modules.Identity_Access.Contracts.Auth;

namespace MathInsight.Modules.Identity_Access.Contracts.Accounts;

/// <summary>
/// UC-03. The account is resolved from the caller's access token, so there is deliberately no
/// account id here — this endpoint can only change the caller's own password.
///
/// BR-08 is enforced on NewPassword by the same attributes as StudentRegisterRequest and
/// ConfirmResetPasswordRequest, so a weak password fails model validation with HTTP 400 before
/// the handler runs. CurrentPassword is only checked for presence: it is validated against the
/// stored hash, and applying the policy to it would fail accounts created before the policy.
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    [RegularExpression(AuthValidation.PasswordPattern, ErrorMessage = AuthValidation.PasswordMessage)]
    public string NewPassword { get; set; } = default!;
}

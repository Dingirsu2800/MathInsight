using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class StudentRegisterRequest
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = default!;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    [RegularExpression(AuthValidation.PasswordPattern, ErrorMessage = AuthValidation.PasswordMessage)]
    public string Password { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = default!;

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? School { get; set; }

    [Range(10, 12)]
    public int? CurrentGrade { get; set; }
}

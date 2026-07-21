using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

// Multipart form: sent as [FromForm] because it carries the certificate file.
public class TeacherRegisterRequest
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

    public string? Biography { get; set; }

    /// <summary>The teacher credential file (JPG/PNG only, ≤ 10 MB — enforced in storage, BR-05).</summary>
    [Required]
    public IFormFile Certificate { get; set; } = default!;
}

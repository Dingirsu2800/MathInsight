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

    /// <summary>
    /// Contact number, written to Account.PhoneNumber at confirmation. Required for teachers so
    /// an Admin can reach the applicant while reviewing the application (UC-08).
    /// </summary>
    [Required]
    [MaxLength(20)]
    [RegularExpression(AuthValidation.PhoneNumberPattern, ErrorMessage = AuthValidation.PhoneNumberMessage)]
    public string PhoneNumber { get; set; } = default!;

    public string? Biography { get; set; }

    /// <summary>
    /// The teacher credential files (JPG/PNG/PDF/DOC/DOCX, ≤ 10 MB each — enforced in storage, BR-05).
    /// At least one is required; the client sends repeated <c>Certificates</c> form entries.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one certificate image is required.")]
    [MaxLength(MaxCertificates, ErrorMessage = "At most 6 certificate images can be uploaded.")]
    public List<IFormFile> Certificates { get; set; } = [];

    /// <summary>Mirrors the client-side cap; keeps the body inside the endpoint's size limit.</summary>
    public const int MaxCertificates = 6;
}

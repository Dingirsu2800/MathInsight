using System.ComponentModel.DataAnnotations;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.Identity_Access.Contracts.Teacher;

/// <summary>
/// UC-08. Edit of a rejected application, sent as multipart because it carries new certificate
/// files. Validation mirrors <see cref="TeacherRegisterRequest"/> so the register and edit paths
/// cannot drift apart.
///
/// Email is absent by design: it is the account's identity and every persisted email is a confirmed
/// one (DD-01), so changing it would require re-verification. Username and password are likewise
/// out of scope here.
/// </summary>
public class UpdateMyApplicationRequest
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = default!;

    [Required]
    [MaxLength(20)]
    [RegularExpression(AuthValidation.PhoneNumberPattern, ErrorMessage = AuthValidation.PhoneNumberMessage)]
    public string PhoneNumber { get; set; } = default!;

    public string? Biography { get; set; }

    /// <summary>
    /// The already-stored certificate URLs the teacher chose to keep. Anything currently on the
    /// application and NOT listed here is dropped. The server accepts only URLs that are already on
    /// this application — a client cannot inject arbitrary URLs into DocumentsUrl through this field.
    /// </summary>
    public List<string> KeptDocumentsUrls { get; set; } = [];

    /// <summary>Newly uploaded certificate images (BR-05: JPG/PNG, ≤ 10 MB each).</summary>
    public List<IFormFile> Certificates { get; set; } = [];
}

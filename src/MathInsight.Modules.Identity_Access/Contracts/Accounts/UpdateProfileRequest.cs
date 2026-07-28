using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Accounts;

/// <summary>
/// UC-05. The editable subset of the caller's own profile.
///
/// PARTIAL update: only the fields present in the request are written. A field that is omitted
/// or sent as null keeps its stored value — so a null can never clear a stored value through
/// this endpoint.
///
/// Username and email are absent by design and cannot be changed here: role changes are UC-17
/// (Admin only), and an email change requires re-verification (separate endpoint) because under
/// DD-01 every persisted email is a confirmed one. Unknown JSON properties are ignored by the
/// default deserializer, so a client sending "username" or "email" is silently a no-op.
///
/// Validation still applies to whatever IS provided. Lengths mirror the current DB script
/// columns exactly (no schema change).
/// </summary>
public class UpdateProfileRequest
{
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    // --- Student (ignored for other roles) ---

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(100)]
    public string? School { get; set; }

    // CK_Student_CurrentGrade constrains this to 10, 11, or 12. Range still rejects an
    // out-of-range value when one is sent; null (omitted) skips the check.
    [Range(10, 12)]
    public int? CurrentGrade { get; set; }

    // --- Teacher (ignored for other roles) ---
    // Biography maps to NVARCHAR(MAX), so no length cap. isVerified is Admin-controlled
    // (UC-15) and is not accepted here.
    public string? Biography { get; set; }

    // --- Expert (ignored for other roles) ---

    [MaxLength(100)]
    public string? Specialty { get; set; }
}

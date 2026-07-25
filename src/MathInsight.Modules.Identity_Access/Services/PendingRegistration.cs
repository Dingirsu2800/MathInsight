namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// The full self-registration payload held in Redis under <c>pending:register:{token}</c>
/// until the user confirms their email (BR-04). No row is written to SQL until confirmation.
/// The password is stored as a BCrypt hash — never in plaintext (BR-08).
/// </summary>
public sealed record PendingRegistration
{
    public required string Username { get; init; }
    public required string Email { get; init; }

    /// <summary>BCrypt hash of the password (BR-08). Never the raw password.</summary>
    public required string PasswordHash { get; init; }

    /// <summary>Target role: "Student" (UC-39) or "Teacher" (UC-08).</summary>
    public required string Role { get; init; }

    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    // Student-specific fields (UC-39).
    public string? Gender { get; init; }
    public string? School { get; init; }
    public int? CurrentGrade { get; init; }

    // Teacher-specific fields (UC-08).
    public string? Biography { get; init; }

    /// <summary>
    /// URL of the teacher certificate uploaded at registration time (BR-05). The
    /// TeacherApplication row referencing it is created only at confirmation.
    /// </summary>
    public string? DocumentsUrl { get; init; }
}

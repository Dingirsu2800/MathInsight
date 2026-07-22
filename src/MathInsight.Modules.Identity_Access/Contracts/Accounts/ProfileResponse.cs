namespace MathInsight.Modules.Identity_Access.Contracts.Accounts;

/// <summary>
/// UC-04. The caller's own profile. Role-specific blocks are populated only for the role the
/// account actually holds; the other two stay null.
/// </summary>
public sealed record ProfileResponse(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? AvatarUrl,
    string RoleName,
    StudentProfile? Student,
    TeacherProfile? Teacher,
    ExpertProfile? Expert);

public sealed record StudentProfile(
    string? Gender,
    string? School,
    int? CurrentGrade);

/// <summary>
/// <paramref name="ApplicationStatus"/> is the status of the teacher's most recent
/// TeacherApplication (Pending/Approved/Rejected), or null when no application exists —
/// e.g. a Teacher created by an Admin (UC-11) never files one. ReviewComments is only
/// meaningful for a Rejected application.
/// </summary>
public sealed record TeacherProfile(
    string? Biography,
    bool IsVerified,
    string? ApplicationStatus,
    string? ReviewComments);

public sealed record ExpertProfile(
    string? Specialty);

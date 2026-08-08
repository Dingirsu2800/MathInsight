namespace MathInsight.Modules.Identity_Access.Contracts.Teacher;

/// <summary>
/// UC-08. The caller's own teacher application, shaped for the self-service view/edit screen.
///
/// Unlike the Admin-facing TeacherApplicationDetailResponse this splits the stored newline-joined
/// DocumentsUrl into <paramref name="DocumentsUrls"/>, because the edit form works with the list
/// (keep some, drop some, add more) rather than the packed column value.
///
/// <paramref name="CanEdit"/> is true only for a Rejected application — the sole state the teacher
/// may edit and resubmit. The server re-checks it; it is sent so the client can render read-only.
/// </summary>
public sealed record MyTeacherApplicationResponse(
    string ApplicationId,
    string TeacherId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Biography,
    IReadOnlyList<string> DocumentsUrls,
    string Status,
    string? ReviewComments,
    DateTime AppliedTime,
    DateTime? ReviewedTime,
    bool CanEdit);

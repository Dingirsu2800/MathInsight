namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

/// <summary>
/// The teacher-application state carried on <see cref="LoginResponse.ApplicationStatus"/> so the
/// client can route an applicant to the application screen instead of the app (BR-06).
///
/// Lowercase on purpose: this is a wire contract consumed by the frontend, deliberately distinct
/// from the title-case values stored in TeacherApplication.Status (CK_TeacherApplication_Status).
/// Null is sent for non-Teacher accounts, for which the field is meaningless.
/// </summary>
public static class LoginApplicationStatus
{
    public const string Pending = "pending";
    public const string Rejected = "rejected";
    public const string Approved = "approved";

    /// <summary>Teacher account with no application row — created by an Admin (UC-11).</summary>
    public const string None = "none";
}

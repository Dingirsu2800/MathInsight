namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string AccountId { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string RoleName { get; set; } = default!;

    /// <summary>
    /// BR-06. For a Teacher: one of <see cref="LoginApplicationStatus"/> — the client sends
    /// "pending"/"rejected" applicants to the application screen and everyone else to their normal
    /// home. Null for Student/Expert/Admin, where the concept does not apply.
    ///
    /// Advisory only: it is NOT what protects teacher features. The "TeacherApproved" policy
    /// re-checks approval against the database on every request.
    /// </summary>
    public string? ApplicationStatus { get; set; }
}


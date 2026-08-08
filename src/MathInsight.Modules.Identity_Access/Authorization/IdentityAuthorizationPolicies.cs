using Microsoft.AspNetCore.Authorization;

namespace MathInsight.Modules.Identity_Access.Authorization;

/// <summary>
/// Authorization policies owned by the Identity_Access module. Registered from the host's
/// <c>AddAuthorization</c> callback so WebAPI never has to name the internal handler type.
/// </summary>
public static class IdentityAuthorizationPolicies
{
    /// <summary>
    /// Role Teacher + an approved application. Use on every endpoint that exposes real teacher
    /// functionality; do NOT use on the self-service application endpoints, which exist precisely
    /// for teachers who are not approved.
    /// </summary>
    public const string TeacherApproved = "TeacherApproved";

    /// <summary>Any authenticated Teacher, approved or not — the application endpoints (UC-08).</summary>
    public const string TeacherApplicant = "TeacherApplicant";

    public const string TeacherRoleName = "Teacher";

    public static AuthorizationOptions AddIdentityAuthorizationPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(TeacherApproved, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new TeacherApprovedRequirement());
        });

        // Deliberately role-only: an applicant's whole purpose is to reach these endpoints while
        // their application is Pending or Rejected. Ownership is enforced in the handlers, which
        // resolve the application from the caller's own account id rather than from the route.
        options.AddPolicy(TeacherApplicant, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(TeacherRoleName);
        });

        return options;
    }
}

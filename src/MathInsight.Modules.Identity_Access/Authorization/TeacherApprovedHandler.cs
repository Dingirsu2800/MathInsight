using System.Security.Claims;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Authorization;

/// <summary>
/// Resolves <see cref="TeacherApprovedRequirement"/> against the database on every request.
///
/// The access token deliberately carries NO application-status claim. Statuses change through an
/// Admin action the token knows nothing about, so a claim would be wrong in both directions for up
/// to the access-token lifetime (DD-02, 15 minutes): an approved teacher would stay locked out, and
/// a teacher whose application was revoked would keep working. Reading the status per request costs
/// one indexed-by-nothing lookup but is always correct.
/// </summary>
public sealed class TeacherApprovedHandler : AuthorizationHandler<TeacherApprovedRequirement>
{
    private readonly IdentityDbContext _dbContext;

    public TeacherApprovedHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TeacherApprovedRequirement requirement)
    {
        // Role first: a Student/Expert/Admin caller never reaches the database. Not calling
        // Succeed is what denies — the requirement fails closed on every early return below.
        if (!context.User.IsInRole(IdentityAuthorizationPolicies.TeacherRoleName))
        {
            return;
        }

        // TokenService writes both claims; NameIdentifier is the configured NameClaimType.
        var accountId = context.User.FindFirstValue("account_id")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        // One round-trip: the Teacher row plus the status of its most recent application, matching
        // the ordering every other consumer uses (LoginCommandHandler, GetProfileQueryHandler).
        var snapshot = await _dbContext.Teachers
            .AsNoTracking()
            .Where(teacher => teacher.TeacherId == accountId)
            .Select(teacher => new
            {
                teacher.IsVerified,
                LatestStatus = _dbContext.TeacherApplications
                    .Where(application => application.TeacherId == teacher.TeacherId)
                    .OrderByDescending(application => application.AppliedTime)
                    .Select(application => application.Status)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync();

        // Role claim says Teacher but no Teacher row exists — a malformed/stale token. Deny.
        if (snapshot is null)
        {
            return;
        }

        // No application at all means the account was created by an Admin (UC-11), which files no
        // application; IsVerified is then the only approval signal. Denying those outright would
        // lock every Admin-created teacher out of the platform.
        var isApproved = snapshot.LatestStatus is null
            ? snapshot.IsVerified
            : string.Equals(
                snapshot.LatestStatus,
                TeacherApplication.StatusApproved,
                StringComparison.OrdinalIgnoreCase);

        if (isApproved)
        {
            context.Succeed(requirement);
        }
    }
}

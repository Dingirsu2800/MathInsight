using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetProfile;

/// <summary>
/// UC-04. Loads the account plus whichever role-specific row exists for it. Read-only.
/// </summary>
public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<ProfileResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public GetProfileQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ProfileResponse>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.Role)
            .Include(account => account.Student)
            .Include(account => account.Teacher)
            .Include(account => account.Expert)
            .FirstOrDefaultAsync(account => account.AccountId == request.AccountId, cancellationToken);

        if (account is null)
        {
            // The token carries an account_id with no row behind it — the account was removed
            // after the token was issued. Treat the token as no longer valid.
            return Result<ProfileResponse>.Failure(AuthErrors.TokenInvalid);
        }

        TeacherProfile? teacher = null;
        if (account.Teacher is not null)
        {
            // BR-06: a Teacher's login eligibility hinges on the application status, so surface it
            // here too. Only the most recent application is relevant.
            var application = await _dbContext.TeacherApplications
                .AsNoTracking()
                .Where(application => application.TeacherId == account.AccountId)
                .OrderByDescending(application => application.AppliedTime)
                .Select(application => new { application.Status, application.ReviewComments })
                .FirstOrDefaultAsync(cancellationToken);

            teacher = new TeacherProfile(
                account.Teacher.Biography,
                account.Teacher.IsVerified,
                application?.Status,
                application?.ReviewComments);
        }

        var profile = new ProfileResponse(
            account.Username,
            account.Email,
            account.FirstName,
            account.LastName,
            account.PhoneNumber,
            account.DateOfBirth,
            account.AvatarUrl,
            account.Role.RoleName,
            account.Student is null
                ? null
                : new StudentProfile(account.Student.Gender, account.Student.School, account.Student.CurrentGrade),
            teacher,
            account.Expert is null
                ? null
                : new ExpertProfile(account.Expert.Specialty));

        return Result<ProfileResponse>.Success(profile);
    }
}

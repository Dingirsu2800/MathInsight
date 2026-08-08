using MathInsight.Modules.Identity_Access.Contracts.Common;
using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetMyTeacherApplication;

public class GetMyTeacherApplicationQueryHandler
    : IRequestHandler<GetMyTeacherApplicationQuery, Result<MyTeacherApplicationResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public GetMyTeacherApplicationQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<MyTeacherApplicationResponse>> Handle(
        GetMyTeacherApplicationQuery request,
        CancellationToken cancellationToken)
    {
        // Most recent first, matching every other consumer (LoginCommandHandler, GetProfile).
        var application = await _dbContext.TeacherApplications
            .AsNoTracking()
            .Include(application => application.Teacher)
            .ThenInclude(teacher => teacher.Account)
            .Where(application => application.TeacherId == request.AccountId)
            .OrderByDescending(application => application.AppliedTime)
            .FirstOrDefaultAsync(cancellationToken);

        // A Teacher created by an Admin (UC-11) files no application — nothing to show.
        if (application is null)
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationNotFound);
        }

        var account = application.Teacher.Account;

        return Result<MyTeacherApplicationResponse>.Success(new MyTeacherApplicationResponse(
            application.ApplicationId,
            application.TeacherId,
            account.Email,
            account.FirstName,
            account.LastName,
            account.PhoneNumber,
            application.Teacher.Biography,
            SplitDocumentsUrl(application.DocumentsUrl),
            application.Status,
            application.ReviewComments,
            UtcTimestamp.AsUtc(application.AppliedTime),
            UtcTimestamp.AsUtc(application.ReviewedTime),
            string.Equals(
                application.Status,
                TeacherApplication.StatusRejected,
                StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Unpacks the newline-joined certificate URLs (BR-05) into the list the form edits.</summary>
    internal static List<string> SplitDocumentsUrl(string? documentsUrl)
    {
        if (string.IsNullOrWhiteSpace(documentsUrl))
        {
            return [];
        }

        return documentsUrl
            .Split(TeacherApplication.DocumentsUrlSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(url => url.Trim())
            .Where(url => url.Length > 0)
            .ToList();
    }
}

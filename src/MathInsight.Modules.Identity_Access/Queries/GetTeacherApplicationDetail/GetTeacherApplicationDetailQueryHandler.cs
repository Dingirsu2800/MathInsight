using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetTeacherApplicationDetail;

public class GetTeacherApplicationDetailQueryHandler
    : IRequestHandler<GetTeacherApplicationDetailQuery, Result<TeacherApplicationDetailResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public GetTeacherApplicationDetailQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TeacherApplicationDetailResponse>> Handle(
        GetTeacherApplicationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.TeacherApplications
            .AsNoTracking()
            .Include(application => application.Teacher)
            .ThenInclude(teacher => teacher.Account)
            .FirstOrDefaultAsync(application => application.ApplicationId == request.ApplicationId, cancellationToken);

        if (application is null)
            return Result<TeacherApplicationDetailResponse>.Failure(IdentityErrors.ApplicationNotFound);

        var account = application.Teacher.Account;

        return Result<TeacherApplicationDetailResponse>.Success(new TeacherApplicationDetailResponse(
            application.ApplicationId,
            application.TeacherId,
            $"{account.FirstName} {account.LastName}",
            account.Email,
            account.PhoneNumber,
            application.Teacher.Biography,
            application.DocumentsUrl,
            application.Status,
            application.ReviewComments,
            application.AppliedTime,
            application.ReviewedTime,
            application.ReviewedBy));
    }
}

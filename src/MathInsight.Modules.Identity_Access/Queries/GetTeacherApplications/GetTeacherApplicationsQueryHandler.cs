using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Contracts.Common;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetTeacherApplications;

public class GetTeacherApplicationsQueryHandler
    : IRequestHandler<GetTeacherApplicationsQuery, Result<PagedResponse<TeacherApplicationListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IdentityDbContext _dbContext;

    public GetTeacherApplicationsQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResponse<TeacherApplicationListItemResponse>>> Handle(
        GetTeacherApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim();

        var query = _dbContext.TeacherApplications
            .AsNoTracking()
            .Include(application => application.Teacher)
            .ThenInclude(teacher => teacher.Account)
            .Where(application => application.Status == status)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        // Projected anonymously first: UtcTimestamp.AsUtc has no SQL translation, so the kind is
        // stamped in memory afterwards. The SQL emitted is unchanged.
        var rows = await query
            .OrderByDescending(application => application.AppliedTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new
            {
                application.ApplicationId,
                application.TeacherId,
                FullName = application.Teacher.Account.FirstName + " " + application.Teacher.Account.LastName,
                application.Teacher.Account.Email,
                application.Status,
                application.AppliedTime,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new TeacherApplicationListItemResponse(
                row.ApplicationId,
                row.TeacherId,
                row.FullName,
                row.Email,
                row.Status,
                UtcTimestamp.AsUtc(row.AppliedTime)))
            .ToList();

        return Result<PagedResponse<TeacherApplicationListItemResponse>>.Success(
            new PagedResponse<TeacherApplicationListItemResponse>(items, pageIndex, pageSize, totalCount, totalPages));
    }
}

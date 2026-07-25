using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Contracts.Common;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Queries.GetAccountList;

public class GetAccountListQueryHandler
    : IRequestHandler<GetAccountListQuery, Result<PagedResponse<AccountListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IdentityDbContext _dbContext;

    public GetAccountListQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResponse<AccountListItemResponse>>> Handle(
        GetAccountListQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var query = _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.RoleName))
            query = query.Where(account => account.Role.RoleName == request.RoleName);

        if (request.IsActive is not null)
            query = query.Where(account => account.IsActive == request.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(account =>
                account.Username.Contains(search) ||
                account.Email.Contains(search) ||
                account.FirstName.Contains(search) ||
                account.LastName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(account => account.CreatedTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(account => new AccountListItemResponse(
                account.AccountId,
                account.Username,
                account.Email,
                account.FirstName,
                account.LastName,
                account.RoleId,
                account.Role.RoleName,
                account.IsActive,
                account.CreatedTime))
            .ToListAsync(cancellationToken);

        return Result<PagedResponse<AccountListItemResponse>>.Success(
            new PagedResponse<AccountListItemResponse>(items, pageIndex, pageSize, totalCount, totalPages));
    }
}

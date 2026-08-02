using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Contracts.Common;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<PagedResponse<NotificationDto>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly NotificationDbContext _dbContext;

    public GetNotificationsQueryHandler(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResponse<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == request.AccountId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.NotificationId,
                n.Title,
                n.Content,
                n.Link,
                n.IsRead,
                n.CreatedTime))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<PagedResponse<NotificationDto>>.Success(
            new PagedResponse<NotificationDto>(items, pageIndex, pageSize, totalCount, totalPages));
    }
}

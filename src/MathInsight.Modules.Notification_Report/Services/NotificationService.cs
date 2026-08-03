using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Entities;
using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Hubs;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Shared.Results;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(NotificationDbContext dbContext, IHubContext<NotificationHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    public async Task<string> SendAsync(
        string accountId,
        string title,
        string content,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        var createdTime = DateTime.UtcNow;
        var notification = new Notification
        {
            NotificationId = Guid.NewGuid().ToString(),
            UserId = accountId,
            Title = title,
            Content = content,
            Link = link,
            IsRead = false,
            CreatedTime = createdTime
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var payload = new NotificationDto(
            notification.NotificationId,
            notification.Title,
            notification.Content,
            notification.Link,
            notification.IsRead,
            notification.CreatedTime);

        // No-op if the account has no active SignalR connection — the DB row above is what makes
        // the notification visible on next GET /api/v1/notifications (BR-22 offline delivery).
        await _hubContext.Clients.User(accountId)
            .SendAsync("ReceiveNotification", payload, cancellationToken);

        return notification.NotificationId;
    }

    public async Task<Result<bool>> MarkReadAsync(
        string notificationId,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, cancellationToken);

        if (notification is null)
        {
            return Result<bool>.Failure(NotificationErrors.NotificationNotFound);
        }

        if (notification.UserId != accountId)
        {
            return Result<bool>.Failure(NotificationErrors.NotificationAccessForbidden);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}

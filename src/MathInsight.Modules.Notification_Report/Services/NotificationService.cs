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
        CancellationToken cancellationToken = default,
        string? deduplicationKey = null)
    {
        if (!string.IsNullOrWhiteSpace(deduplicationKey))
        {
            var existing = await _dbContext.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UserId == accountId && item.DeduplicationKey == deduplicationKey,
                    cancellationToken);
            if (existing is not null)
                return existing.NotificationId;
        }

        var createdTime = DateTime.UtcNow;
        var notification = new Notification
        {
            NotificationId = Guid.NewGuid().ToString(),
            UserId = accountId,
            Title = title,
            Content = content,
            Link = link,
            DeduplicationKey = deduplicationKey,
            IsRead = false,
            CreatedTime = createdTime
        };

        _dbContext.Notifications.Add(notification);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(deduplicationKey))
        {
            // The unique database index is the race-safe authority when two retry workers read
            // before either one inserts. A duplicate row means another worker persisted it.
            _dbContext.Entry(notification).State = EntityState.Detached;
            var existing = await _dbContext.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.UserId == accountId && item.DeduplicationKey == deduplicationKey,
                    cancellationToken);
            if (existing is not null)
                return existing.NotificationId;
            throw;
        }

        var payload = new NotificationDto(
            notification.NotificationId,
            notification.Title,
            notification.Content,
            notification.Link,
            notification.IsRead,
            notification.CreatedTime);

        // No-op if the account has no active SignalR connection — the DB row above is what makes
        // the notification visible on next GET /api/v1/notifications (BR-22 offline delivery).
        try
        {
            await _hubContext.Clients.User(accountId)
                .SendAsync("ReceiveNotification", payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The persisted inbox entry is the delivery guarantee; SignalR is best effort.
        }

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

using MathInsight.Shared.Results;

namespace MathInsight.Modules.Notification_Report.Services;

public interface INotificationService
{
    /// <summary>
    /// BR-22: persists a Notification record and pushes it over SignalR to the target account if
    /// currently connected. Returns the new notification id.
    /// </summary>
    Task<string> SendAsync(
        string accountId,
        string title,
        string content,
        string? link = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification read. Fails with NotificationErrors.NotificationNotFound or
    /// NotificationAccessForbidden when the notification does not belong to accountId.
    /// </summary>
    Task<Result<bool>> MarkReadAsync(
        string notificationId,
        string accountId,
        CancellationToken cancellationToken = default);
}

using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

public sealed class NotificationRequestedHandler : INotificationHandler<NotificationRequestedEvent>
{
    private readonly INotificationService _notificationService;

    public NotificationRequestedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(NotificationRequestedEvent notification, CancellationToken cancellationToken) =>
        _notificationService.SendAsync(
            notification.AccountId,
            notification.Title,
            notification.Content,
            notification.Link,
            cancellationToken,
            notification.DeduplicationKey);
}

namespace MathInsight.Modules.Notification_Report.Contracts;

/// <summary>
/// Shape returned by GET /api/v1/notifications and pushed as the SignalR "ReceiveNotification"
/// payload — a freshly-pushed notification is always unread, so NotificationService constructs
/// this with IsRead = false.
/// </summary>
public sealed record NotificationDto(
    string NotificationId,
    string Title,
    string Content,
    string? Link,
    bool IsRead,
    DateTime CreatedTime);

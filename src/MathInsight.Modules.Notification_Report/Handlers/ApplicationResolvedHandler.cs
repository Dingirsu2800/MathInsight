using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>
/// UC-92. Notifies the teacher whose application was approved or rejected. TeacherId is the
/// Teacher's shared-PK id, which equals its AccountId (see Teacher/Account FK), so it can be used
/// directly as the notification recipient. In-app only — ApplicationResolvedEvent does not carry
/// an email address, so no welcome-style email is sent here (out of scope for this module until
/// Identity enriches the event).
/// </summary>
public sealed class ApplicationResolvedHandler : INotificationHandler<ApplicationResolvedEvent>
{
    private readonly INotificationService _notificationService;

    public ApplicationResolvedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(ApplicationResolvedEvent notification, CancellationToken cancellationToken)
    {
        var title = notification.Approved ? "Application Approved" : "Application Rejected";
        var content = string.IsNullOrWhiteSpace(notification.ReviewComments)
            ? title
            : notification.ReviewComments!;

        return _notificationService.SendAsync(
            notification.TeacherId,
            title,
            content,
            "/profile",
            cancellationToken);
    }
}

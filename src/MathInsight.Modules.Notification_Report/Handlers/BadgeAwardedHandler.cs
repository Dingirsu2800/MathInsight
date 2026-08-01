using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-90. Pushes a "badge earned" notification to the student.</summary>
public sealed class BadgeAwardedHandler : INotificationHandler<BadgeAwardedEvent>
{
    private readonly INotificationService _notificationService;

    public BadgeAwardedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(BadgeAwardedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.SendAsync(
            notification.StudentId,
            "Badge Earned! 🏆",
            $"You earned the \"{notification.BadgeName}\" badge!",
            "/student/achievements",
            cancellationToken);
    }
}

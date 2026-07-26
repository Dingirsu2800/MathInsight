using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-90 / BR-45. Pushes a streak reminder to a student at risk of losing their streak.</summary>
public sealed class StreakReminderHandler : INotificationHandler<StreakReminderEvent>
{
    private readonly INotificationService _notificationService;

    public StreakReminderHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(StreakReminderEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.SendAsync(
            notification.StudentId,
            "🔥 Don't break your streak!",
            $"Complete a lesson today to keep your {notification.CurrentStreak}-day streak alive.",
            "/dashboard",
            cancellationToken);
    }
}

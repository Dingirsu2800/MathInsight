using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-89. Pushes a "test graded" notification to the student.</summary>
public sealed class GradeCalculatedHandler : INotificationHandler<GradeCalculatedEvent>
{
    private readonly INotificationService _notificationService;

    public GradeCalculatedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(GradeCalculatedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.SendAsync(
            notification.StudentId,
            "Test Graded",
            $"Your test has been graded: {notification.Score:0.##}/10.",
            $"/sessions/{notification.SessionId}/solution",
            cancellationToken);
    }
}

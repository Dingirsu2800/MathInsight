using MathInsight.Modules.Learning_Lecture.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-90. Notifies the student that a teacher answered their discussion question.</summary>
public sealed class DiscussionAnsweredHandler : INotificationHandler<DiscussionAnsweredEvent>
{
    private readonly INotificationService _notificationService;

    public DiscussionAnsweredHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(DiscussionAnsweredEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.SendAsync(
            notification.StudentId,
            "Answer Received",
            "A teacher replied to your question.",
            $"/student/lectures/{notification.LectureId}",
            cancellationToken);
    }
}

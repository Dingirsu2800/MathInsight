using MathInsight.Modules.Learning_Lecture.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-90. Notifies the lecture's teacher that a student posted a new discussion question.</summary>
public sealed class DiscussionQuestionPostedHandler : INotificationHandler<DiscussionQuestionPostedEvent>
{
    private readonly INotificationService _notificationService;

    public DiscussionQuestionPostedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(DiscussionQuestionPostedEvent notification, CancellationToken cancellationToken)
    {
        return _notificationService.SendAsync(
            notification.TeacherId,
            "New Question",
            $"A student asked: \"{notification.Title}\"",
            $"/teacher/lectures/{notification.LectureId}#discussions",
            cancellationToken);
    }
}

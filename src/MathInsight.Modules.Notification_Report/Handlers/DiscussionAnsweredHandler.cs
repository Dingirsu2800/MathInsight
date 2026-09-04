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
        if (notification.AccountId == notification.StudentId)
        {
            // Học sinh tự phản hồi -> Gửi thông báo cho Giáo viên
            if (string.IsNullOrEmpty(notification.TeacherId)) return Task.CompletedTask;
            
            return _notificationService.SendAsync(
                notification.TeacherId,
                "Có phản hồi mới",
                "Một học sinh vừa phản hồi trong phần thảo luận bài giảng.",
                $"/teacher/lectures/{notification.LectureId}?discussionId={notification.DiscussionAnswerId}#discussions",
                cancellationToken);
        }
        else
        {
            // Giáo viên trả lời -> Gửi thông báo cho Học sinh
            return _notificationService.SendAsync(
                notification.StudentId,
                "Đã nhận câu trả lời",
                "Giáo viên vừa trả lời câu hỏi của bạn.",
                $"/student/lectures/{notification.LectureId}?discussionId={notification.DiscussionAnswerId}#discussions",
                cancellationToken);
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>
/// Notifies the student that their discussion comment was hidden by a moderator.
/// </summary>
public sealed class DiscussionCommentHiddenHandler : INotificationHandler<DiscussionCommentHiddenEvent>
{
    private readonly INotificationService _notificationService;

    public DiscussionCommentHiddenHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(DiscussionCommentHiddenEvent notification, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrEmpty(notification.ModerationReason) 
            ? "Không có lý do cụ thể." 
            : notification.ModerationReason;

        var message = $"Bình luận của bạn đã bị ẩn do vi phạm cộng đồng. Lý do: {reason}";

        return _notificationService.SendAsync(
            notification.TargetAccountId,
            "Bình luận bị ẩn",
            message,
            $"/student/lectures/{notification.LectureId}#discussions",
            cancellationToken);
    }
}

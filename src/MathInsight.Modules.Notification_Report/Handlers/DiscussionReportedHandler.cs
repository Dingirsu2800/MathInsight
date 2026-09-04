using MathInsight.Modules.Learning_Lecture.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MathInsight.Modules.Notification_Report.Handlers;

public sealed class DiscussionReportedHandler : INotificationHandler<DiscussionReportedEvent>
{
    private readonly INotificationService _notificationService;

    public DiscussionReportedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(DiscussionReportedEvent notification, CancellationToken cancellationToken)
    {
        string targetName = notification.TargetType == "Question" ? "câu hỏi" : "câu trả lời";
        return _notificationService.SendAsync(
            notification.TeacherId,
            "Có báo cáo vi phạm",
            $"Một {targetName} trong bài giảng của bạn đã bị báo cáo vì: {notification.Reason}",
            "/teacher/moderation",
            cancellationToken);
    }
}

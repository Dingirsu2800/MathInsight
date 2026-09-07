using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

public sealed class BlueprintReviewedHandler : INotificationHandler<BlueprintReviewedEvent>
{
    private readonly INotificationService _notificationService;

    public BlueprintReviewedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(BlueprintReviewedEvent notification, CancellationToken cancellationToken)
    {
        var approved = string.Equals(notification.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        var content = approved
            ? "Cấu trúc đề của bạn đã được duyệt."
            : $"Cấu trúc đề của bạn bị từ chối{(string.IsNullOrWhiteSpace(notification.ReviewNote) ? "." : $": {notification.ReviewNote}")}";
        return _notificationService.SendAsync(
            notification.OwnerExpertId,
            approved ? "Cấu trúc đề đã được duyệt" : "Cấu trúc đề cần chỉnh sửa",
            content,
            $"/expert/blueprints/{notification.BlueprintId}",
            cancellationToken,
            $"blueprint:{notification.BlueprintId}:review:{notification.Status}");
    }
}

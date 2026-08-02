using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    private readonly INotificationService _notificationService;

    public MarkNotificationReadCommandHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task<Result<bool>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
        => _notificationService.MarkReadAsync(request.NotificationId, request.AccountId, cancellationToken);
}

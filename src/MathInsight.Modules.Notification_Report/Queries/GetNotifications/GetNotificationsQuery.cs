using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Contracts.Common;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    string AccountId,
    bool UnreadOnly,
    int PageIndex,
    int PageSize) : IRequest<Result<PagedResponse<NotificationDto>>>;

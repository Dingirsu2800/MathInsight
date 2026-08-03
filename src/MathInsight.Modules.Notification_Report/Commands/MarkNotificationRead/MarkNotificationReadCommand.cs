using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(string NotificationId, string AccountId) : IRequest<Result<bool>>;

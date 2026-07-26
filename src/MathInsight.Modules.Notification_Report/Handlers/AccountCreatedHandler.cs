using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Notification_Report.Services;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-92. Sends the in-app welcome notification and welcome email for a new account.</summary>
public sealed class AccountCreatedHandler : INotificationHandler<AccountCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public AccountCreatedHandler(INotificationService notificationService, IEmailService emailService)
    {
        _notificationService = notificationService;
        _emailService = emailService;
    }

    public async Task Handle(AccountCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _notificationService.SendAsync(
            notification.AccountId,
            "Welcome to MathInsight!",
            $"Hi {notification.FirstName}, your account is ready.",
            "/dashboard",
            cancellationToken);

        await _emailService.SendWelcomeEmailAsync(notification.Email, notification.FirstName, cancellationToken);
    }
}

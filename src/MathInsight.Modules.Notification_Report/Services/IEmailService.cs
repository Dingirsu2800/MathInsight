namespace MathInsight.Modules.Notification_Report.Services;

public interface IEmailService
{
    /// <summary>UC-92: welcome email sent when AccountCreatedEvent fires.</summary>
    Task SendWelcomeEmailAsync(string email, string firstName, CancellationToken cancellationToken = default);
}

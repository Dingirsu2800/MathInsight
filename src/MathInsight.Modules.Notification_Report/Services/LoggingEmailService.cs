using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Notification_Report.Services;

/// <summary>
/// Fallback email service that logs instead of sending. Selected when SMTP is disabled or not
/// configured, so local development runs without SMTP credentials.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendWelcomeEmailAsync(string email, string firstName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Email fallback] Welcome email for {Email} ({FirstName}).", email, firstName);
        return Task.CompletedTask;
    }
}

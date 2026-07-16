using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Fallback email service that logs the confirmation/reset link instead of sending a real email.
/// Selected when SMTP is disabled or not configured, so local development runs without SMTP
/// credentials. Logs the same clickable link the real email would contain.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;
    private readonly string _frontendBaseUrl;

    public LoggingEmailService(ILogger<LoggingEmailService> logger, string frontendBaseUrl)
    {
        _logger = logger;
        _frontendBaseUrl = frontendBaseUrl;
    }

    public Task SendRegistrationConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken)
    {
        var link = EmailLinkBuilder.ConfirmEmail(_frontendBaseUrl, confirmationToken);
        _logger.LogInformation("[Email fallback] Registration confirmation for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken)
    {
        var link = EmailLinkBuilder.ResetPassword(_frontendBaseUrl, resetToken);
        _logger.LogInformation("[Email fallback] Password reset for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }
}

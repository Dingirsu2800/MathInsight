using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MathInsight.Modules.Notification_Report.Services;

/// <summary>
/// Real email delivery over SMTP using MailKit. Selected when <c>Smtp:Enabled = true</c> and a
/// host is configured; otherwise the module falls back to <see cref="LoggingEmailService"/>.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(SmtpOptions options, ILogger<SmtpEmailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task SendWelcomeEmailAsync(string email, string firstName, CancellationToken cancellationToken = default)
    {
        var body =
            $"<p>Welcome to MathInsight, {firstName}!</p>" +
            $"<p>Your account is ready. Log in to start exploring lectures, practice tests, and badges.</p>";

        return SendAsync(email, "Welcome to MathInsight", body, cancellationToken);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var socketOptions = _options.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send email '{Subject}' to {Recipient} via SMTP.", subject, to);
            throw;
        }
    }
}

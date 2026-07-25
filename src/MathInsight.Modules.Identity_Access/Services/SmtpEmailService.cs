using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Real email delivery over SMTP using MailKit. Selected when <c>Smtp:Enabled = true</c> and a
/// host is configured; otherwise the app falls back to <see cref="LoggingEmailService"/>.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(SmtpOptions options, string frontendBaseUrl, ILogger<SmtpEmailService> logger)
    {
        _options = options;
        _frontendBaseUrl = frontendBaseUrl;
        _logger = logger;
    }

    public Task SendRegistrationConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken)
    {
        var link = EmailLinkBuilder.ConfirmEmail(_frontendBaseUrl, confirmationToken);
        var body =
            $"<p>Welcome to MathInsight!</p>" +
            $"<p>Please confirm your account by clicking the link below:</p>" +
            $"<p><a href=\"{link}\">Confirm my account</a></p>" +
            $"<p>This link expires in 24 hours. If you did not sign up, you can ignore this email.</p>";

        return SendAsync(email, "Confirm your MathInsight account", body, cancellationToken);
    }

    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken)
    {
        var link = EmailLinkBuilder.ResetPassword(_frontendBaseUrl, resetToken);
        var body =
            $"<p>We received a request to reset your MathInsight password.</p>" +
            $"<p><a href=\"{link}\">Reset my password</a></p>" +
            $"<p>If you did not request this, you can ignore this email.</p>";

        return SendAsync(email, "Reset your MathInsight password", body, cancellationToken);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        // Auto negotiates TLS/STARTTLS by port when SSL is enabled; None for a plain local relay.
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

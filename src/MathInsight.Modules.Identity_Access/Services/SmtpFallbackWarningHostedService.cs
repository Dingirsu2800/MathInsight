using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Logs a single warning at startup when the app falls back to <see cref="LoggingEmailService"/>
/// (SMTP disabled or not configured), so it is obvious that confirmation emails are only logged,
/// not delivered. Registered only when the fallback is in effect.
/// </summary>
public class SmtpFallbackWarningHostedService : IHostedService
{
    private readonly ILogger<SmtpFallbackWarningHostedService> _logger;

    public SmtpFallbackWarningHostedService(ILogger<SmtpFallbackWarningHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "SMTP is disabled or not configured (Smtp:Enabled=false or Smtp:Host empty). " +
            "Using the logging email fallback — confirmation and password-reset emails are logged, not sent. " +
            "Configure the Smtp section (credentials via user secrets or environment variables) to enable real delivery.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

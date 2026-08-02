using MathInsight.Modules.Notification_Report.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Notification_Report.BackgroundJobs;

/// <summary>
/// BR-21. Daily timer that prunes notifications older than 90 days. Lightweight built-in
/// <see cref="BackgroundService"/>, mirroring Gamification's StreakReminderBackgroundService —
/// Hangfire is referenced by the host but not wired anywhere in the solution yet, so scheduled
/// jobs use this pattern instead of introducing new infrastructure.
///
/// Config (host appsettings; both optional, so nothing runs unless explicitly enabled):
///   Notification:Prune:Enabled     bool, default false — job does nothing when absent/false.
///   Notification:Prune:RunAtUtcHour int 0-23, default 1 — matches the spec's 01:00 daily cron.
/// </summary>
public class NotificationPruneBackgroundService : BackgroundService
{
    private const int DefaultRunAtUtcHour = 1;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationPruneBackgroundService> _logger;

    public NotificationPruneBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationPruneBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("Notification:Prune");

        if (!section.GetValue<bool>("Enabled"))
        {
            _logger.LogInformation(
                "NotificationPrune job disabled (Notification:Prune:Enabled is false or absent); not scheduling.");
            return;
        }

        var runAtUtcHour = section.GetValue<int?>("RunAtUtcHour") ?? DefaultRunAtUtcHour;
        if (runAtUtcHour is < 0 or > 23)
        {
            _logger.LogWarning(
                "NotificationPrune RunAtUtcHour {Configured} is out of range; falling back to {Default}.",
                runAtUtcHour, DefaultRunAtUtcHour);
            runAtUtcHour = DefaultRunAtUtcHour;
        }

        _logger.LogInformation(
            "NotificationPrune job enabled; runs daily at {Hour:00}:00 UTC.", runAtUtcHour);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTime.UtcNow, runAtUtcHour);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // host is shutting down
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<NotificationPruneJob>();

                var count = await job.RunAsync(stoppingToken);

                _logger.LogInformation("NotificationPrune run complete: {Count} notification(s) deleted.", count);
            }
            catch (OperationCanceledException)
            {
                break; // host is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationPrune run failed.");
            }
        }
    }

    private static TimeSpan DelayUntilNextRun(DateTime utcNow, int runAtUtcHour)
    {
        var todayRun = new DateTime(
            utcNow.Year, utcNow.Month, utcNow.Day, runAtUtcHour, 0, 0, DateTimeKind.Utc);

        var nextRun = utcNow < todayRun ? todayRun : todayRun.AddDays(1);
        return nextRun - utcNow;
    }
}

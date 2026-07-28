using MathInsight.Modules.Gamification.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Gamification.BackgroundJobs;

/// <summary>
/// BR-45. Daily timer that triggers streak-reminder detection. Lightweight built-in
/// <see cref="BackgroundService"/> (Hangfire is optional per the spec).
///
/// Config (host appsettings; both optional, so nothing runs unless explicitly enabled):
///   Gamification:StreakReminder:Enabled     bool, default false — job does nothing when absent/false.
///   Gamification:StreakReminder:RunAtUtcHour int 0–23, default 13 — 13:00 UTC = 20:00 Vietnam (UTC+7).
///
/// MVP simplification: the spec's "20:00 local time" is collapsed to a single configurable UTC hour.
/// Real per-student multi-timezone handling is out of scope for the MVP.
/// </summary>
public class StreakReminderBackgroundService : BackgroundService
{
    private const int DefaultRunAtUtcHour = 13; // 20:00 in UTC+7 (Vietnam)

    // A BackgroundService is a SINGLETON, so it must not depend on the scoped GamificationDbContext /
    // IStreakReminderService directly. It resolves a fresh scope per run via the scope factory.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StreakReminderBackgroundService> _logger;

    public StreakReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StreakReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("Gamification:StreakReminder");

        // Absent section => GetValue<bool> returns false => job is disabled by default.
        if (!section.GetValue<bool>("Enabled"))
        {
            _logger.LogInformation(
                "StreakReminder job disabled (Gamification:StreakReminder:Enabled is false or absent); not scheduling.");
            return;
        }

        var runAtUtcHour = section.GetValue<int?>("RunAtUtcHour") ?? DefaultRunAtUtcHour;
        if (runAtUtcHour is < 0 or > 23)
        {
            _logger.LogWarning(
                "StreakReminder RunAtUtcHour {Configured} is out of range; falling back to {Default}.",
                runAtUtcHour, DefaultRunAtUtcHour);
            runAtUtcHour = DefaultRunAtUtcHour;
        }

        _logger.LogInformation(
            "StreakReminder job enabled; runs daily at {Hour:00}:00 UTC.", runAtUtcHour);

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
                var reminderService = scope.ServiceProvider.GetRequiredService<IStreakReminderService>();

                var count = await reminderService.SendRemindersAsync(
                    DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);

                _logger.LogInformation(
                    "StreakReminder run complete: {Count} reminder(s) published.", count);
            }
            catch (OperationCanceledException)
            {
                break; // host is shutting down
            }
            catch (Exception ex)
            {
                // A single failed run must not tear down the service — log and wait for the next tick.
                _logger.LogError(ex, "StreakReminder run failed.");
            }
        }
    }

    /// <summary>Time from <paramref name="utcNow"/> until the next <paramref name="runAtUtcHour"/>:00 UTC.</summary>
    private static TimeSpan DelayUntilNextRun(DateTime utcNow, int runAtUtcHour)
    {
        var todayRun = new DateTime(
            utcNow.Year, utcNow.Month, utcNow.Day, runAtUtcHour, 0, 0, DateTimeKind.Utc);

        var nextRun = utcNow < todayRun ? todayRun : todayRun.AddDays(1);
        return nextRun - utcNow;
    }
}

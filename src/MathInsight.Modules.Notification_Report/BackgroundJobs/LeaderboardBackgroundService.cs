using MathInsight.Modules.Notification_Report.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Notification_Report.BackgroundJobs;

/// <summary>
/// BR-19. Daily timer that recalculates the leaderboard cache. Lightweight built-in
/// <see cref="BackgroundService"/>, same reasoning as NotificationPruneBackgroundService —
/// Hangfire is referenced by the host but not wired anywhere in the solution.
///
/// Config (host appsettings; both optional, so nothing runs unless explicitly enabled):
///   Notification:LeaderboardJob:Enabled     bool, default false.
///   Notification:LeaderboardJob:RunAtUtcHour int 0-23, default 0 — matches the spec's 00:00 daily cron.
///
/// The leaderboard endpoint itself also writes-through to cache on a miss (see
/// GetLeaderboardQueryHandler), so a disabled job only means the cache is populated lazily by
/// the first read of the day instead of proactively at midnight.
/// </summary>
public class LeaderboardBackgroundService : BackgroundService
{
    private const int DefaultRunAtUtcHour = 0;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LeaderboardBackgroundService> _logger;

    public LeaderboardBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<LeaderboardBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("Notification:LeaderboardJob");

        if (!section.GetValue<bool>("Enabled"))
        {
            _logger.LogInformation(
                "LeaderboardRecalculation job disabled (Notification:LeaderboardJob:Enabled is false or absent); not scheduling.");
            return;
        }

        var runAtUtcHour = section.GetValue<int?>("RunAtUtcHour") ?? DefaultRunAtUtcHour;
        if (runAtUtcHour is < 0 or > 23)
        {
            _logger.LogWarning(
                "LeaderboardRecalculation RunAtUtcHour {Configured} is out of range; falling back to {Default}.",
                runAtUtcHour, DefaultRunAtUtcHour);
            runAtUtcHour = DefaultRunAtUtcHour;
        }

        _logger.LogInformation(
            "LeaderboardRecalculation job enabled; runs daily at {Hour:00}:00 UTC.", runAtUtcHour);

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
                var job = scope.ServiceProvider.GetRequiredService<LeaderboardRecalculationJob>();

                var count = await job.RunAsync(stoppingToken);

                _logger.LogInformation("LeaderboardRecalculation run complete: {Count} entrie(s) cached.", count);
            }
            catch (OperationCanceledException)
            {
                break; // host is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeaderboardRecalculation run failed.");
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

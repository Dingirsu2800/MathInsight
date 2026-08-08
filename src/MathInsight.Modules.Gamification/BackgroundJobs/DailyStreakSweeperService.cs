using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Gamification.Persistence;

namespace MathInsight.Modules.Gamification.BackgroundJobs;

public class DailyStreakSweeperService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyStreakSweeperService> _logger;

    public DailyStreakSweeperService(IServiceProvider serviceProvider, ILogger<DailyStreakSweeperService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyStreakSweeperService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            
            // Calculate time until next 00:00 AM (UTC+7)
            // Vietnam Time (UTC+7)
            var vnTimeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+7", new TimeSpan(7, 0, 0), "UTC+7", "UTC+7");
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var nextMidnightVn = nowVn.Date.AddDays(1);
            var delay = nextMidnightVn - nowVn;

            _logger.LogInformation("Next streak sweep scheduled in {DelayHours} hours, {DelayMinutes} minutes.", delay.Hours, delay.Minutes);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await SweepExpiredStreaksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sweeping expired streaks.");
            }
        }
    }

    public async Task SweepExpiredStreaksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var todayVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.CreateCustomTimeZone("UTC+7", new TimeSpan(7, 0, 0), "UTC+7", "UTC+7")).Date;
        var cutoffDate = DateOnly.FromDateTime(todayVn.AddDays(-1));

        _logger.LogInformation("Sweeping streaks with LastActivityDate strictly earlier than {CutoffDate}", cutoffDate);

        var expiredStreaks = await dbContext.StudyStreaks
            .Where(s => s.LastActivityDate < cutoffDate && s.CurrentStreak > 0)
            .ToListAsync(cancellationToken);

        if (expiredStreaks.Any())
        {
            foreach (var streak in expiredStreaks)
            {
                // Reset streak to 0 as they missed the previous day entirely
                streak.CurrentStreak = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully reset {Count} expired streaks to 0.", expiredStreaks.Count);
        }
        else
        {
            _logger.LogInformation("No expired streaks found today.");
        }
    }
}

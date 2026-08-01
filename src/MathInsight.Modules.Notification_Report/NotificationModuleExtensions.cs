using MathInsight.Modules.Notification_Report.BackgroundJobs;
using MathInsight.Modules.Notification_Report.Jobs;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Notification_Report;

public static class NotificationModuleExtensions
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register NotificationDbContext using the shared SQL Server connection.
        // This module owns: Notification. Do NOT add EF migrations — table structure is managed
        // by DB scripts.
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        // MediatR in-process handlers: GetNotificationsQueryHandler, MarkNotificationReadCommandHandler,
        // and the 7 domain-event handlers, all in this assembly.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(NotificationModuleExtensions).Assembly));

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationPruneJob>();

        // Email delivery: real SMTP (MailKit) when configured, otherwise a logging fallback so
        // local development runs without SMTP credentials. Own SmtpOptions binding — this module
        // does not reference Identity_Access's email infrastructure.
        var smtpOptions = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();

        if (smtpOptions.Enabled && !string.IsNullOrWhiteSpace(smtpOptions.Host))
        {
            services.AddScoped<IEmailService>(serviceProvider => new SmtpEmailService(
                smtpOptions,
                serviceProvider.GetRequiredService<ILogger<SmtpEmailService>>()));
        }
        else
        {
            services.AddScoped<IEmailService>(serviceProvider => new LoggingEmailService(
                serviceProvider.GetRequiredService<ILogger<LoggingEmailService>>()));
        }

        // BR-21: daily notification prune. Disabled by default — see NotificationPruneBackgroundService.
        services.AddHostedService<NotificationPruneBackgroundService>();

        // BR-19: leaderboard cache. Redis is optional — AddSharedRedis is idempotent, so this is
        // safe alongside Identity_Access's own conditional registration.
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            services.AddSharedRedis(configuration);
            services.AddSingleton<ILeaderboardCacheService, RedisLeaderboardCacheService>();
        }
        else
        {
            services.AddSingleton<ILeaderboardCacheService, NullLeaderboardCacheService>();
        }

        services.AddScoped<LeaderboardRecalculationJob>();

        // BR-19: daily leaderboard recalculation. Disabled by default — see LeaderboardBackgroundService.
        services.AddHostedService<LeaderboardBackgroundService>();

        return services;
    }
}

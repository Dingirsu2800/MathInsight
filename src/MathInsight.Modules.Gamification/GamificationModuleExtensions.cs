using MathInsight.Modules.Gamification.BackgroundJobs;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MathInsight.Modules.Gamification;

public static class GamificationModuleExtensions
{
    public static IServiceCollection AddGamificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register GamificationDbContext using the shared SQL Server connection.
        // This module owns: ActivityLog, StudyStreak, Badge, StudentBadge, TargetScore.
        // Do NOT add EF migrations — table structure is managed by DB scripts.
        services.AddDbContext<GamificationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        // MediatR in-process handlers: ActivityLoggedHandler (ActivityLoggedEvent) and
        // TestSubmittedHandler (TestSubmittedEvent), both in this assembly.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GamificationModuleExtensions).Assembly));

        // Domain services
        services.AddScoped<IStreakService, StreakService>();

        // BR-43: BadgeService implemented by Student B
        services.AddScoped<IBadgeService, BadgeService>();

        // BR-45: daily streak-reminder detection. The service is scoped (DbContext + MediatR); the
        // hosted BackgroundService is a singleton that opens a scope per run. Disabled by default —
        // it does nothing unless Gamification:StreakReminder:Enabled = true in configuration.
        services.AddScoped<IStreakReminderService, StreakReminderService>();
        services.AddHostedService<StreakReminderBackgroundService>();

        return services;
    }
}

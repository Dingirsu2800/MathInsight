using MathInsight.Modules.Gamification.Persistence;
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

        // MediatR handlers, services, and controllers are added in a later step.
        return services;
    }
}

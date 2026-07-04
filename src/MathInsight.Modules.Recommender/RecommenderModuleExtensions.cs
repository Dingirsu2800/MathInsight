using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MathInsight.Modules.Recommender.Persistence;

namespace MathInsight.Modules.Recommender;

public static class RecommenderModuleExtensions
{
    public static IServiceCollection AddRecommenderModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register RecommenderDbContext using the shared SQL Server connection.
        // This module owns: CompetencyPoint, TagsMastery, StudentTopicSessionResult.
        // Do NOT add EF migrations — table structure is managed by DB scripts.
        services.AddDbContext<RecommenderDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        // Phase 2: CompetencyEngine, DifficultyMappingService, RecommenderService, MediatR handlers.
        // Phase 3: RecommenderController registered via MapControllers in WebAPI.

        return services;
    }
}

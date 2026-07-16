using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Recommendation;

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

        // MediatR in-process handlers (replaces deleted MassTransit GradeCalculatedConsumer).
        // TopicResultIngestionHandler handles GradeCalculatedEvent from Grading module (004).
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(RecommenderModuleExtensions).Assembly));

        // Domain services
        services.AddScoped<ICompetencyEngine, CompetencyEngine>();
        services.AddSingleton<IDifficultyMappingService, DifficultyMappingService>();
        services.AddScoped<RecommenderService>();
        services.AddScoped<IRecommenderService>(provider =>
            provider.GetRequiredService<RecommenderService>());
        services.AddScoped<IStudentRecommendationProvider>(provider =>
            provider.GetRequiredService<RecommenderService>());

        // RecommenderController is auto-discovered by AddControllers() in WebAPI.

        return services;
    }
}

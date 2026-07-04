using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MathInsight.Modules.Grading_Analytics.Persistence;

namespace MathInsight.Modules.Grading_Analytics;

public static class GradingModuleExtensions
{
    public static IServiceCollection AddGradingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register GradingDbContext using the shared SQL Server connection.
        // This module does NOT own any tables — cross-reads Testing and QuestionBank tables only.
        // Do NOT add EF migrations from this context.
        services.AddDbContext<GradingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        // Phase 2: GradingEngine, ChatbotService, and MediatR handlers will be registered here.

        return services;
    }
}

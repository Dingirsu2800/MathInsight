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
        //
        // U2 (Polly retry spec requirement): EF Core EnableRetryOnFailure covers transient DB
        // failures (connection drops, deadlocks, timeout) with 3 retries and exponential backoff.
        // No additional Polly package is needed for MVP — this is the equivalent mechanism.
        services.AddDbContext<GradingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));


        // GradingEngine: per-question-type grading algorithm (Phase 2)
        services.AddScoped<Services.IGradingEngine, Services.GradingEngine>();

        // Phase 2 (remaining): ChatbotService, GradeSubmittedSessionHandler will be registered here.

        return services;
    }
}

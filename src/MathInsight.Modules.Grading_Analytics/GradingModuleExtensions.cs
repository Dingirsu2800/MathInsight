using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Services;
using Polly;
using Polly.CircuitBreaker;

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

        // GradingEngine: per-question-type grading algorithm
        services.AddScoped<Services.IGradingEngine, Services.GradingEngine>();

        // ── ChatbotService (UC-51) ───────────────────────────────────────────
        // Bind Chatbot configuration section
        services.Configure<ChatbotOptions>(configuration.GetSection(ChatbotOptions.SectionName));

        // Register HttpClient for ChatbotService with:
        //   - 10-second timeout per request
        //   - Polly circuit breaker: 3 consecutive failures = open for 30s
        services.AddHttpClient<IChatbotService, ChatbotService>((sp, client) =>
        {
            var chatbotOptions = configuration.GetSection(ChatbotOptions.SectionName).Get<ChatbotOptions>()
                ?? new ChatbotOptions();

            client.BaseAddress = new Uri(chatbotOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddResilienceHandler("chatbot-pipeline", builder =>
        {
            // Circuit breaker: open after 3 failures, stay open for 30 seconds.
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,       // 100% failure rate threshold
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3,    // 3 failures before opening
                BreakDuration = TimeSpan.FromSeconds(30),
            });
        });

        // MediatR handlers (GradeSubmittedSessionHandler) are registered via
        // AddMediatR in WebAPI's Program.cs scanning all module assemblies.
        // No explicit registration needed here if the assembly is included in the scan.

        return services;
    }
}

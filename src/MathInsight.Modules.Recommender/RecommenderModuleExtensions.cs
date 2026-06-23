using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Recommender;

public static class RecommenderModuleExtensions
{
    public static IServiceCollection AddRecommenderModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "rcm"
        
        // Register services, repositories, handlers
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Gamification;

public static class GamificationModuleExtensions
{
    public static IServiceCollection AddGamificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "gam"
        
        // Register services, repositories, handlers
        return services;
    }
}

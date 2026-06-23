using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Grading_Analytics;

public static class GradingModuleExtensions
{
    public static IServiceCollection AddGradingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "grd"
        
        // Register services, repositories, handlers
        return services;
    }
}

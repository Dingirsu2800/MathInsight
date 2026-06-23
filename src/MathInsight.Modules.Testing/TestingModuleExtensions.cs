using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Testing;

public static class TestingModuleExtensions
{
    public static IServiceCollection AddTestingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "tst"
        
        // Register services, repositories, handlers
        return services;
    }
}

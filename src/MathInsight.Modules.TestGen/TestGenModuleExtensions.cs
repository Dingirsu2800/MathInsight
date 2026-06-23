using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.TestGen;

public static class TestGenModuleExtensions
{
    public static IServiceCollection AddTestGenModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "tst" (shared schema with Testing module)
        
        // Register services, repositories, blueprint validators, co-occurrence resolvers
        return services;
    }
}

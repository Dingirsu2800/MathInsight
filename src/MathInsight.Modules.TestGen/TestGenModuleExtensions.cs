using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MathInsight.Modules.TestGen.Persistence;

namespace MathInsight.Modules.TestGen;

public static class TestGenModuleExtensions
{
    public static IServiceCollection AddTestGenModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with shared connection (shared schema with Testing module)
        services.AddDbContext<TestGenDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));
        
        // Register services, repositories, blueprint validators, co-occurrence resolvers
        return services;
    }
}

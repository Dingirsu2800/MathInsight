using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Testing.Persistence;

namespace MathInsight.Modules.Testing;

public static class TestingModuleExtensions
{
    public static IServiceCollection AddTestingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TestingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));
        
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(TestingModuleExtensions).Assembly);
        });

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MathInsight.Modules.Testing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing;

public static class TestingModuleExtensions
{
    public static IServiceCollection AddTestingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TestingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure()));
        return services;
    }
}

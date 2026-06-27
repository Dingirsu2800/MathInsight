using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MathInsight.Shared.Caching;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddSharedRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return services;
        }

        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis:ConnectionString is not configured.");
        }

        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));

        return services;
    }
}

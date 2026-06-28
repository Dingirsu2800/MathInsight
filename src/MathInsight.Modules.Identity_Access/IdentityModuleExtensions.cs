using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Identity_Access.Services.Auth;
using MathInsight.Shared.Caching;

namespace MathInsight.Modules.Identity_Access;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(IdentityModuleExtensions).Assembly);
        });

        services.AddScoped<ITokenService, TokenService>();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            services.AddSharedRedis(configuration);
            services.AddScoped<IAuthSessionService, RedisAuthSessionService>();
        }
        else
        {
            services.AddSingleton<IAuthSessionService, InMemoryAuthSessionService>();
        }

        return services;
    }
}

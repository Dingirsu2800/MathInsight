using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "usr"
        // builder.Services.AddDbContext<IdentityDbContext>(options => ...);
        
        // Register services, repositories, Cloudinary image upload client
        return services;
    }
}
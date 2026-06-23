using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Notification_Report;

public static class NotificationModuleExtensions
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "ntf"
        
        // Register services, repositories, handlers
        return services;
    }
}

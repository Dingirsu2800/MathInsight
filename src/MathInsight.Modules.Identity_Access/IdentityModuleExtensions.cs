using MathInsight.Modules.Identity_Access.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        // Teacher certificates are stored via the shared blob/image storage (BR-05).
        services.AddScoped<ICertificateStorage, BlobCertificateStorage>();

        // Email delivery: real SMTP (MailKit) when configured, otherwise a logging fallback so
        // local development runs without SMTP credentials.
        var smtpOptions = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
        var frontendBaseUrl = configuration["FrontendBaseUrl"] ?? string.Empty;

        if (smtpOptions.Enabled && !string.IsNullOrWhiteSpace(smtpOptions.Host))
        {
            services.AddScoped<IEmailService>(serviceProvider => new SmtpEmailService(
                smtpOptions,
                frontendBaseUrl,
                serviceProvider.GetRequiredService<ILogger<SmtpEmailService>>()));
        }
        else
        {
            services.AddScoped<IEmailService>(serviceProvider => new LoggingEmailService(
                serviceProvider.GetRequiredService<ILogger<LoggingEmailService>>(),
                frontendBaseUrl));

            // Warn once at startup that emails are only logged, not delivered.
            services.AddHostedService<SmtpFallbackWarningHostedService>();
        }

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            services.AddSharedRedis(configuration);
            services.AddScoped<IAuthSessionService, RedisAuthSessionService>();

            // Pending registrations live only in Redis (BR-04); no fallback store exists.
            services.AddScoped<IPendingRegistrationStore, RedisPendingRegistrationStore>();

            // Password-reset tokens (UC-06, password:reset:{token}, 15m TTL).
            services.AddScoped<IPasswordResetTokenStore, RedisPasswordResetTokenStore>();
        }
        else
        {
            services.AddSingleton<IAuthSessionService, InMemoryAuthSessionService>();

            // In-memory fallback so registration works without Redis (local dev). Singleton so
            // pending registrations persist across requests; they are lost on restart.
            services.AddSingleton<IPendingRegistrationStore, InMemoryPendingRegistrationStore>();

            // In-memory fallback for reset tokens; singleton so they survive across requests.
            services.AddSingleton<IPasswordResetTokenStore, InMemoryPasswordResetTokenStore>();
        }

        return services;
    }
}

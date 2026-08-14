using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathInsight.WebAPI.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real WebAPI pipeline — routing, model binding, JWT authentication, the authorization
/// policies, MediatR and the module handlers — with only the external infrastructure swapped out:
///
///   • all nine SQL Server DbContexts → EF Core InMemory, one isolated store per factory instance
///   • Redis                          → the module's own in-memory fallbacks (Redis:Enabled=false)
///   • RabbitMQ                       → MassTransit's in-memory transport (RabbitMQ:Enabled=false)
///   • SMTP                           → <see cref="CapturingEmailService"/>, so tokens are readable
///   • Google OAuth                   → <see cref="FakeGoogleOAuthService"/>
///   • Cloudinary                     → <see cref="FakeImageStorage"/>
///
/// Everything a controller test asserts on — status codes, response bodies, auth and role checks,
/// and the resulting database state — is produced by the real pipeline.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string StudentRoleId = "44444444-4444-4444-4444-444444444444";
    public const string TeacherRoleId = "33333333-3333-3333-3333-333333333333";
    public const string ExpertRoleId = "22222222-2222-2222-2222-222222222222";
    public const string AdminRoleId = "11111111-1111-1111-1111-111111111111";

    public const string SigningKey = "integration-test-signing-key-that-is-long-enough-for-hmac-sha256";
    public const string Issuer = "MathInsight.Test";
    public const string Audience = "MathInsight.Test.Client";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public CapturingEmailService Emails { get; } = new();
    public FakeGoogleOAuthService Google { get; } = new();
    public FakeImageStorage Images { get; } = new();
    public StreakReminderRecorder StreakReminders { get; } = new();

    /// <summary>Resolves a scoped service from the running host, e.g. IMediator or IStreakReminderService.</summary>
    public async Task WithScopedAsync<TService>(Func<TService, Task> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<TResult> FromScopedAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>
    /// Program.cs reads configuration EAGERLY while registering services (Redis:Enabled decides
    /// whether the Redis multiplexer is registered at all, Jwt:SigningKey throws if absent). Under
    /// the minimal hosting model those reads happen before WebApplicationFactory's
    /// ConfigureAppConfiguration delegates are applied, so the only settings source that lands in
    /// time is the environment — which builder.Configuration picks up at construction.
    /// </summary>
    static AuthApiFactory()
    {
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = "Server=(unused);Database=Test;",
            ["Jwt__SigningKey"] = SigningKey,
            ["Jwt__Issuer"] = Issuer,
            ["Jwt__Audience"] = Audience,
            ["Redis__Enabled"] = "false",     // routes to the module's in-memory token/session stores
            ["RabbitMQ__Enabled"] = "false",  // MassTransit in-memory transport
            ["Smtp__Enabled"] = "false",
            ["FrontendBaseUrl"] = "https://app.test",
            ["GoogleOAuth__ClientId"] = "test-client-id",
            ["GoogleOAuth__ClientSecret"] = "test-client-secret",
            ["GoogleOAuth__RedirectUri"] = "https://api.test/api/v1/auth/google/callback",
        };

        foreach (var (key, value) in settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            ReplaceWithInMemory<IdentityDbContext>(services);
            ReplaceWithInMemory<QuestionBankDbContext>(services);
            ReplaceWithInMemory<TestingDbContext>(services);
            ReplaceWithInMemory<TestGenDbContext>(services);
            ReplaceWithInMemory<GradingDbContext>(services);
            ReplaceWithInMemory<RecommenderDbContext>(services);
            ReplaceWithInMemory<LearningDbContext>(services);
            ReplaceWithInMemory<GamificationDbContext>(services);
            ReplaceWithInMemory<NotificationDbContext>(services);

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            services.RemoveAll<IGoogleOAuthService>();
            services.AddSingleton<IGoogleOAuthService>(Google);

            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage>(Images);

            // Added, not substituted: MediatR fans a notification out to every handler, so this
            // observes the real StreakReminderEvent publication without displacing the consumer.
            services.AddSingleton(StreakReminders);
            services.AddSingleton<MediatR.INotificationHandler<MathInsight.Shared.Events.StreakReminderEvent>>(
                StreakReminders);
        });
    }

    private void ReplaceWithInMemory<TContext>(IServiceCollection services) where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();

        // Built OUTSIDE the application container on purpose. Calling AddDbContext again would let
        // EF resolve its internal services from the app's provider, which by then already carries
        // the SQL Server provider registered by the module — the "only a single database provider
        // can be registered" failure. A self-contained options object has no such ambiguity.
        var options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(_databaseName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        services.AddSingleton(options);
        services.AddScoped(_ => (TContext)Activator.CreateInstance(typeof(TContext), options)!);
    }

    /// <summary>Runs work against the Identity store, e.g. to seed or assert on rows.</summary>
    public async Task WithIdentityDbAsync(Func<IdentityDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await action(db);
    }

    public async Task<TResult> FromIdentityDbAsync<TResult>(Func<IdentityDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await action(db);
    }

    /// <summary>Runs work against the Gamification store, e.g. to seed or assert on StudyStreak rows.</summary>
    public async Task WithGamificationDbAsync(Func<GamificationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        await action(db);
    }

    public async Task<TResult> FromGamificationDbAsync<TResult>(Func<GamificationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        return await action(db);
    }

    /// <summary>Ensures the four seeded roles exist (they may already come from RoleConfiguration.HasData).</summary>
    public Task EnsureRolesAsync() => WithIdentityDbAsync(async db =>
    {
        var wanted = new (string Id, string Name)[]
        {
            (AdminRoleId, "Admin"), (ExpertRoleId, "Expert"), (TeacherRoleId, "Teacher"), (StudentRoleId, "Student")
        };

        foreach (var (id, name) in wanted)
        {
            if (!await db.Roles.AnyAsync(role => role.RoleId == id))
            {
                db.Roles.Add(new Role { RoleId = id, RoleName = name });
            }
        }

        await db.SaveChangesAsync();
    });
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Testing.Persistence;

namespace MathInsight.Modules.Testing;

public static class TestingModuleExtensions
{
    /// <summary>
    /// Registers all Testing module services: EF Core DbContext, MediatR command/query handlers,
    /// and module-specific controllers (discovered automatically via AddControllers in WebAPI).
    /// </summary>
    public static IServiceCollection AddTestingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Persistence: Testing DbContext with retry-on-failure for transient SQL errors.
        services.AddDbContext<TestingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        // Application layer: register all IRequestHandler<,> implementations in this assembly.
        // Covers: StartSessionCommandHandler, AutoSaveCommandHandler, RecordIncidentCommandHandler,
        // SubmitSessionCommandHandler, ForceSubmitSessionCommandHandler,
        // ReportSessionQuestionCommandHandler, GetDetailedSolutionQueryHandler.
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(TestingModuleExtensions).Assembly);
        });

        return services;
    }
}


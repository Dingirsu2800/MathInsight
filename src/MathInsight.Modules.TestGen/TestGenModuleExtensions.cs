using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Modules.TestGen.Generation;

namespace MathInsight.Modules.TestGen;

public static class TestGenModuleExtensions
{
    public static IServiceCollection AddTestGenModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TestGenDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(TestGenModuleExtensions).Assembly);
        });

        services.AddScoped<IBlueprintAggregateValidator, BlueprintAggregateValidator>();
        services.AddScoped<IBlueprintExamCandidateProvider, BlueprintExamCandidateProvider>();
        services.AddScoped<IBlueprintExamQuestionSelector, CapacityAwareQuestionSelector>();
        services.AddSingleton<IGenerationRandomizer, SystemGenerationRandomizer>();

        return services;
    }
}

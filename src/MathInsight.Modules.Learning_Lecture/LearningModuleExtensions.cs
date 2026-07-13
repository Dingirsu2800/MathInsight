using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Services;

namespace MathInsight.Modules.Learning_Lecture;

public static class LearningModuleExtensions
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LearningDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(LearningModuleExtensions).Assembly);
        });

        services.AddScoped<ICloudinaryService, CloudinaryService>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.Learning_Lecture;

public static class LearningModuleExtensions
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "lrn"
        
        // Register services, repositories, handlers
        return services;
    }
}

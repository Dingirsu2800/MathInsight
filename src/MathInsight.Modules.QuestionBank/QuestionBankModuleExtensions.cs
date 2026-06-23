using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MathInsight.Modules.QuestionBank;

public static class QuestionBankModuleExtensions
{
    public static IServiceCollection AddQuestionBankModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext with Schema "qnb"
        
        // Register services, repositories, handlers
        return services;
    }
}

using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Ocr;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MathInsight.Modules.QuestionBank;

public static class QuestionBankModuleExtensions
{
    public static IServiceCollection AddQuestionBankModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuestionBankDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services
            .AddOptions<MistralOcrOptions>()
            .Bind(configuration.GetSection(MistralOcrOptions.SectionName));
        services.AddHttpClient<IQuestionOcrService, MistralQuestionOcrService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(QuestionBankModuleExtensions).Assembly);
        });

        return services;
    }
}

using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Ocr;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MathInsight.Modules.QuestionBank;

public static class QuestionBankModuleExtensions
{
    public static IServiceCollection AddQuestionBankModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Question-bank mutations use explicit serializable transactions and SQL locks. Do not
        // configure automatic EF retries here: these commands are not request-idempotent, so a
        // retry after an unknown commit outcome could replay a successful mutation.
        services.AddDbContext<QuestionBankDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services
            .AddOptions<MistralOcrOptions>()
            .Bind(configuration.GetSection(MistralOcrOptions.SectionName));
        services.AddHttpClient<IQuestionOcrService, MistralQuestionOcrService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IQuestionImportWorkbookParser, QuestionImportWorkbookParser>();
        services.AddScoped<IQuestionImportTemplateService, QuestionImportTemplateService>();
        services.AddScoped<QuestionImportValidationService>();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(QuestionBankModuleExtensions).Assembly);
        });

        return services;
    }
}

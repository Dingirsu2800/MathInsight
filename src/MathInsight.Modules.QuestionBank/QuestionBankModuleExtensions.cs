using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Storage;
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
            .AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddHttpClient<IQuestionImageStorage, CloudinaryQuestionImageStorage>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(QuestionBankModuleExtensions).Assembly);
        });

        return services;
    }
}

using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

internal sealed class QuestionBankInMemoryContext : IAsyncDisposable
{
    private QuestionBankInMemoryContext(QuestionBankDbContext context)
    {
        Context = context;
    }

    public QuestionBankDbContext Context { get; }

    public static Task<QuestionBankInMemoryContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseInMemoryDatabase($"question-bank-tests-{Guid.NewGuid()}")
            .Options;

        return Task.FromResult(new QuestionBankInMemoryContext(new QuestionBankDbContext(options)));
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

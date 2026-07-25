using MathInsight.Modules.Learning_Lecture.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Learning_Lecture.Tests;

internal sealed class LearningInMemoryContext : IAsyncDisposable
{
    private LearningInMemoryContext(LearningDbContext context)
    {
        Context = context;
    }

    public LearningDbContext Context { get; }

    public static Task<LearningInMemoryContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase($"learning-lecture-tests-{Guid.NewGuid()}")
            .Options;

        return Task.FromResult(new LearningInMemoryContext(new LearningDbContext(options)));
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

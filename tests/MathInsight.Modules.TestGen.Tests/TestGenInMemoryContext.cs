using MathInsight.Modules.TestGen.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

internal sealed class TestGenInMemoryContext : IAsyncDisposable
{
    private TestGenInMemoryContext(TestGenDbContext context)
    {
        Context = context;
    }

    public TestGenDbContext Context { get; }

    public static TestGenInMemoryContext Create()
    {
        var options = new DbContextOptionsBuilder<TestGenDbContext>()
            .UseInMemoryDatabase($"testgen-tests-{Guid.NewGuid()}")
            .Options;

        return new TestGenInMemoryContext(new TestGenDbContext(options));
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

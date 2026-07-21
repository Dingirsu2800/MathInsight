using MathInsight.Modules.Testing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Tests;

/// <summary>
/// Creates an isolated EF Core InMemory TestingDbContext for each test run.
/// Each call to Create() generates a unique database name to prevent cross-test contamination.
/// </summary>
internal sealed class TestingInMemoryContext : IAsyncDisposable
{
    private TestingInMemoryContext(TestingDbContext context)
    {
        Context = context;
    }

    public TestingDbContext Context { get; }

    public static TestingInMemoryContext Create()
    {
        var options = new DbContextOptionsBuilder<TestingDbContext>()
            .UseInMemoryDatabase($"testing-tests-{Guid.NewGuid()}")
            .Options;

        return new TestingInMemoryContext(new TestingDbContext(options));
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}

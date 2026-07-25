using MathInsight.Modules.Testing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.StartSession;

internal static class TestSqlServerLock
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    public static bool IsSupported(TestingDbContext context)
        => string.Equals(
            context.Database.ProviderName,
            SqlServerProvider,
            StringComparison.Ordinal);

    public static async Task LockAsync(
        TestingDbContext context,
        string testId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Test] WITH (UPDLOCK, HOLDLOCK) WHERE [TestID] = {testId}",
            cancellationToken);
    }
}

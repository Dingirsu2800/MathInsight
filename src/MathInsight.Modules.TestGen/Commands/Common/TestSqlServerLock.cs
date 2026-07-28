using MathInsight.Modules.TestGen.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Commands.Common;

internal static class TestSqlServerLock
{
    public static async Task LockAsync(
        TestGenDbContext context,
        string testId,
        CancellationToken cancellationToken)
    {
        if (!BlueprintSqlServerLock.IsSupported(context))
            return;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Test] WITH (UPDLOCK, HOLDLOCK) WHERE [TestID] = {testId}",
            cancellationToken);
    }
}

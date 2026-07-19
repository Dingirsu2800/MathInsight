using MathInsight.Modules.TestGen.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Commands.Common;

internal static class BlueprintSqlServerLock
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    public static bool IsSupported(TestGenDbContext context)
        => string.Equals(
            context.Database.ProviderName,
            SqlServerProvider,
            StringComparison.Ordinal);

    public static async Task LockAsync(
        TestGenDbContext context,
        string blueprintId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Blueprint] WITH (UPDLOCK, HOLDLOCK) WHERE [BlueprintID] = {blueprintId}",
            cancellationToken);
    }
}

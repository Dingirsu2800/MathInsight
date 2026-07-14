using MathInsight.Modules.TestGen.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.Common;

internal static class BlueprintExecutionStrategy
{
    public static Task<TResult> ExecuteAsync<TResult>(
        TestGenDbContext context,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!BlueprintSqlServerLock.IsSupported(context))
            return operation();

        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;

        return strategy.ExecuteAsync(
            operation,
            async (_, currentOperation, _) =>
            {
                if (attempt++ > 0)
                    context.ChangeTracker.Clear();

                return await currentOperation();
            },
            verifySucceeded: null,
            cancellationToken);
    }
}

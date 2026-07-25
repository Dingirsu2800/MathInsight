using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;

internal static class TestGenerationExecutionStrategy
{
    public static Task<TResult> ExecuteAsync<TResult>(
        TestGenDbContext context,
        Func<Task<TResult>> operation,
        Func<Task<(bool IsSuccessful, TResult Result)>> verifySucceeded,
        CancellationToken cancellationToken)
    {
        if (!BlueprintSqlServerLock.IsSupported(context))
            return operation();

        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;
        var state = (Operation: operation, VerifySucceeded: verifySucceeded);

        return strategy.ExecuteAsync(
            state,
            async (_, currentState, _) =>
            {
                if (attempt++ > 0)
                    context.ChangeTracker.Clear();

                return await currentState.Operation();
            },
            async (_, currentState, _) =>
            {
                context.ChangeTracker.Clear();
                var verification = await currentState.VerifySucceeded();
                return new ExecutionResult<TResult>(verification.IsSuccessful, verification.Result);
            },
            cancellationToken);
    }
}

using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

internal static class QuestionReportSqlServerLock
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    public static bool IsSupported(QuestionBankDbContext context)
    {
        return string.Equals(
            context.Database.ProviderName,
            SqlServerProvider,
            StringComparison.Ordinal);
    }

    public static async Task LockQuestionAsync(
        QuestionBankDbContext context,
        string questionId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Question] WITH (UPDLOCK, HOLDLOCK) WHERE [QuestionID] = {questionId}",
            cancellationToken);
    }
}

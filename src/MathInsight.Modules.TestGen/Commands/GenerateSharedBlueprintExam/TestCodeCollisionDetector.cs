using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;

internal static class TestCodeCollisionDetector
{
    private const string TestCodeIndex = "UX_Test_TestCode_NotNull";

    public static bool IsTestCodeCollision(DbUpdateException exception)
        => exception.InnerException is SqlException sqlException &&
           sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627) &&
           sqlException.Message.Contains(TestCodeIndex, StringComparison.OrdinalIgnoreCase);
}

internal sealed class TestCodeCollisionException : Exception;

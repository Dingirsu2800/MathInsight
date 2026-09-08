using Microsoft.Data.SqlClient;

namespace MathInsight.WebAPI.Infrastructure;

public static class DatabaseSchemaExceptionClassifier
{
    public const string ErrorCode = "DATABASE_SCHEMA_OUTDATED";
    public const string ErrorMessage =
        "The database schema is not compatible with this application version. Apply the required database migration.";

    public static bool IsSchemaMismatch(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                IsSchemaMismatch(sqlException.Number, sqlException.Message))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsSchemaMismatch(int number, string? message)
    {
        // SQL Server 207/208 are invalid-column/object errors. They indicate that
        // the deployed application and database schema are out of sync.
        return number is 207 or 208;
    }
}

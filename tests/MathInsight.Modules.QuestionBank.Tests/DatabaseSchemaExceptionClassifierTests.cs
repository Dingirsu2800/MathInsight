using MathInsight.WebAPI.Infrastructure;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class DatabaseSchemaExceptionClassifierTests
{
    [Theory]
    [InlineData(207, "Invalid column name 'QuestionVersionID'.")]
    [InlineData(208, "Invalid object name 'QuestionReportIncident'.")]
    public void SqlServerSchemaErrors_AreClassifiedAsOutdated(int number, string message)
    {
        Assert.True(DatabaseSchemaExceptionClassifier.IsSchemaMismatch(number, message));
    }

    [Theory]
    [InlineData(547, "The INSERT statement conflicted with the FOREIGN KEY constraint.")]
    [InlineData(50000, "A domain validation error occurred.")]
    public void UnrelatedSqlErrors_AreNotClassifiedAsOutdated(int number, string message)
    {
        Assert.False(DatabaseSchemaExceptionClassifier.IsSchemaMismatch(number, message));
    }
}

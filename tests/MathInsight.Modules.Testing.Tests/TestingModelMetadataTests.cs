using MathInsight.Modules.Testing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MathInsight.Modules.Testing.Tests;

public sealed class TestingModelMetadataTests
{
    private readonly IModel _model;

    public TestingModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<TestingDbContext>()
            .UseSqlServer("Server=localhost;Database=MathInsightMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new TestingDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(TestReadModel), "Test")]
    [InlineData(typeof(TestQuestionReadModel), "TestQuestion")]
    [InlineData(typeof(QuestionVersionReadModel), "QuestionVersion")]
    [InlineData(typeof(TestSession), "TestSession")]
    [InlineData(typeof(TestAnswer), "TestAnswer")]
    [InlineData(typeof(TestAnswerOption), "TestAnswerOption")]
    [InlineData(typeof(TestAnswerPart), "TestAnswerPart")]
    public void SharedSqlTables_AreExcludedFromTestingMigrations(Type clrType, string tableName)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(clrType));

        Assert.Equal(tableName, entity.GetTableName());
        Assert.True(entity.IsTableExcludedFromMigrations());
    }

    [Fact]
    public void ScoreColumns_UseFiveTwoPrecision()
    {
        AssertPrecision<TestReadModel>(nameof(TestReadModel.MaxScore));
        AssertPrecision<TestQuestionReadModel>(nameof(TestQuestionReadModel.MaxPointsSnapshot));
        AssertPrecision<TestSession>(nameof(TestSession.Score));
        AssertPrecision<TestAnswer>(nameof(TestAnswer.PointsEarned));
        AssertPrecision<TestAnswerPart>(nameof(TestAnswerPart.PointsEarned));
    }

    private void AssertPrecision<TEntity>(string propertyName)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));
        var property = Assert.IsAssignableFrom<IProperty>(entity.FindProperty(propertyName));
        Assert.Equal(5, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }
}

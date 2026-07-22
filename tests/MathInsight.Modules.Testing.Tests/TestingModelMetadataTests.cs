using MathInsight.Modules.Testing.Persistence;
using MathInsight.Modules.Testing.Entities;
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
    [InlineData(typeof(Test), "Test")]
    [InlineData(typeof(TestQuestion), "TestQuestion")]
    [InlineData(typeof(QuestionVersion), "QuestionVersion")]
    public void CrossModuleReadTables_AreExcludedFromTestingMigrations(Type clrType, string tableName)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(clrType));

        Assert.Equal(tableName, entity.GetTableName());
        Assert.True(entity.IsTableExcludedFromMigrations());
    }

    [Fact]
    public void ScoreColumns_UseFiveTwoPrecision()
    {
        AssertPrecision<Test>(nameof(Test.MaxScore), 5, 2);
        AssertPrecision<TestQuestion>(nameof(TestQuestion.WeightSnapshot), 5, 2);
        AssertPrecision<TestQuestion>(nameof(TestQuestion.MaxPointsSnapshot), 5, 2);
        AssertPrecision<TestSession>(nameof(TestSession.Score), 5, 2);
        AssertPrecision<TestAnswer>(nameof(TestAnswer.PointsEarned), 4, 2);
        AssertPrecision<TestAnswerPart>(nameof(TestAnswerPart.PointsEarned), 4, 2);
    }

    private void AssertPrecision<TEntity>(string propertyName, int precision, int scale)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));
        var property = Assert.IsAssignableFrom<IProperty>(entity.FindProperty(propertyName));
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }
}

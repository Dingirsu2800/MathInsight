using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MathInsight.Modules.Grading_Analytics.Tests;

public sealed class GradingModelMetadataTests
{
    private readonly IModel _model;

    public GradingModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseSqlServer("Server=localhost;Database=MathInsightMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new GradingDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void AllMappedTables_AreExcludedFromGradingMigrations()
    {
        Assert.All(_model.GetEntityTypes(), entity => Assert.True(entity.IsTableExcludedFromMigrations()));
    }

    [Fact]
    public void ScoreAndRevisionColumns_MatchSqlContract()
    {
        AssertPrecision<TestSession>(nameof(TestSession.Score));
        AssertPrecision<TestAnswer>(nameof(TestAnswer.PointsEarned));
        AssertPrecision<TestAnswerPart>(nameof(TestAnswerPart.PointsEarned));
        AssertPrecision<TestQuestion>(nameof(TestQuestion.MaxPointsSnapshot));

        var session = Entity<TestSession>();
        Assert.Equal("GradeRevision", Property(session, nameof(TestSession.GradeRevision)).GetColumnName());

        var version = Entity<QuestionVersion>();
        Assert.Equal("SnapshotSchemaVersion", Property(version, nameof(QuestionVersion.SnapshotSchemaVersion)).GetColumnName());
    }

    private IEntityType Entity<TEntity>()
        => Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));

    private static IProperty Property(IEntityType entity, string propertyName)
        => Assert.IsAssignableFrom<IProperty>(entity.FindProperty(propertyName));

    private void AssertPrecision<TEntity>(string propertyName)
    {
        var property = Property(Entity<TEntity>(), propertyName);
        Assert.Equal(5, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }
}

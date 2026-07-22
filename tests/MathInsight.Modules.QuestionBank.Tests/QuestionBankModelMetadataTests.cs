using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionBankModelMetadataTests
{
    private readonly IModel _model;

    public QuestionBankModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseSqlServer("Server=localhost;Database=MathInsightMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new QuestionBankDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void QuestionAndChildren_MatchCurrentWeightAndArchiveContract()
    {
        var question = Entity<Question>();
        AssertDecimalFiveTwo(question, nameof(Question.DefaultWeight));
        Assert.Equal("datetime2(0)", Property(question, nameof(Question.CreatedTime)).GetColumnType());
        Assert.Equal("datetime2(0)", Property(question, nameof(Question.UpdatedTime)).GetColumnType());

        var answer = Entity<Answer>();
        Assert.Equal("IsArchived", Property(answer, nameof(Answer.IsArchived)).GetColumnName());
        Assert.Contains(answer.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_Answer_Current_Question" &&
            index.GetFilter() == "[IsArchived] = 0");

        var part = Entity<QuestionPart>();
        AssertDecimalFiveTwo(part, nameof(QuestionPart.DefaultWeight));
        Assert.Equal("IsArchived", Property(part, nameof(QuestionPart.IsArchived)).GetColumnName());
        Assert.Contains(part.GetIndexes(), index =>
            index.GetDatabaseName() == "UX_QuestionPart_Current_Order" &&
            index.IsUnique &&
            index.GetFilter() == "[IsArchived] = 0");
        Assert.Contains(part.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_QuestionPart_Current_Question" &&
            index.GetFilter() == "[IsArchived] = 0");
    }

    [Fact]
    public void VersionAndReport_MatchImmutableHistoryContract()
    {
        var version = Entity<QuestionVersion>();
        Assert.Equal("VersionNumber", Property(version, nameof(QuestionVersion.VersionNumber)).GetColumnName());
        Assert.Equal("SnapshotSchemaVersion", Property(version, nameof(QuestionVersion.SnapshotSchemaVersion)).GetColumnName());
        Assert.Contains(version.GetIndexes(), index =>
            index.GetDatabaseName() == "UX_QuestionVersion_Question_VersionNumber" && index.IsUnique);

        var report = Entity<QuestionReport>();
        Assert.Equal("SessionID", Property(report, nameof(QuestionReport.SessionId)).GetColumnName());
        Assert.Equal("QuestionVersionID", Property(report, nameof(QuestionReport.QuestionVersionId)).GetColumnName());
        Assert.Equal("ResolutionAction", Property(report, nameof(QuestionReport.ResolutionAction)).GetColumnName());
        Assert.Equal("datetime2(0)", Property(report, nameof(QuestionReport.ScoreAdjustedTime)).GetColumnType());
    }

    private IEntityType Entity<TEntity>()
        => Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));

    private static IProperty Property(IEntityType entity, string propertyName)
        => Assert.IsAssignableFrom<IProperty>(entity.FindProperty(propertyName));

    private static void AssertDecimalFiveTwo(IEntityType entity, string propertyName)
    {
        var property = Property(entity, propertyName);
        Assert.Equal("decimal(5,2)", property.GetColumnType());
    }
}

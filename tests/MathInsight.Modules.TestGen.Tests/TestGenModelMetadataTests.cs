using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TestEntity = MathInsight.Modules.TestGen.Persistence.Entities.Test;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TestGenModelMetadataTests
{
    private readonly IModel _model;

    public TestGenModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<TestGenDbContext>()
            .UseSqlServer("Server=localhost;Database=MathInsightMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new TestGenDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Blueprint_MatchesSqlContract()
    {
        var entity = Entity<Blueprint>("Blueprint");

        AssertStringColumn(entity, nameof(Blueprint.BlueprintId), "BlueprintID", 36, nullable: false);
        AssertStringColumn(entity, nameof(Blueprint.BlueprintName), "BlueprintName", 100, nullable: false, unicode: true);
        AssertStringColumn(entity, nameof(Blueprint.ExpertId), "ExpertID", 36, nullable: false);
        AssertStringColumn(entity, nameof(Blueprint.Status), "Status", 20, nullable: false);
        AssertStringColumn(entity, nameof(Blueprint.ApprovedBy), "ApprovedBy", 36, nullable: true);
        Assert.Equal("ReviewNote", Column(entity, nameof(Blueprint.ReviewNote)).GetColumnName());
        Assert.Equal("datetime2(0)", Column(entity, nameof(Blueprint.ReviewTime)).GetColumnType());
        Assert.Null(entity.FindProperty("CreatedTime"));

        Assert.Equal("Draft", Column(entity, nameof(Blueprint.Status)).GetDefaultValue());
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_Blueprint_Status");
        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() == "IX_Blueprint_ExpertID");
    }

    [Fact]
    public void BlueprintSection_MatchesSqlContractAndCompositePrincipalKey()
    {
        var entity = Entity<BlueprintSection>("BlueprintSection");

        AssertStringColumn(entity, nameof(BlueprintSection.BlueprintSectionId), "BlueprintSectionID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintSection.BlueprintId), "BlueprintID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintSection.SectionName), "SectionName", 100, nullable: false, unicode: true);
        AssertStringColumn(entity, nameof(BlueprintSection.QuestionType), "QuestionType", 30, nullable: false);
        Assert.False(Column(entity, nameof(BlueprintSection.ScoreBudget)).IsNullable);
        Assert.Equal(5, Column(entity, nameof(BlueprintSection.ScoreBudget)).GetPrecision());
        Assert.Equal(2, Column(entity, nameof(BlueprintSection.ScoreBudget)).GetScale());

        Assert.Contains(
            entity.GetKeys(),
            key => key.GetName() == "UQ_BlueprintSection_ID_Blueprint"
                && PropertyNames(key.Properties).SequenceEqual(
                    [nameof(BlueprintSection.BlueprintSectionId), nameof(BlueprintSection.BlueprintId)]));
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_BlueprintSection_CompositePartMetadata");
        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() == "UQ_BlueprintSection_Blueprint_Order" && x.IsUnique);
    }

    [Fact]
    public void BlueprintDetail_UsesCompositeSectionForeignKey()
    {
        var entity = Entity<BlueprintDetail>("BlueprintDetail");

        AssertStringColumn(entity, nameof(BlueprintDetail.BlueprintDetailId), "BlueprintDetailID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintDetail.BlueprintSectionId), "BlueprintSectionID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintDetail.BlueprintId), "BlueprintID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintDetail.TagId), "TagID", 36, nullable: false);
        AssertStringColumn(entity, nameof(BlueprintDetail.DifficultyId), "DifficultyID", 36, nullable: false);

        Assert.Contains(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.GetConstraintName() == "FK_BlueprintDetail_BlueprintSection_BlueprintSectionID"
                && PropertyNames(foreignKey.Properties).SequenceEqual(
                    [nameof(BlueprintDetail.BlueprintSectionId), nameof(BlueprintDetail.BlueprintId)]));
        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() == "UQ_BlueprintDetail_Section_Tag_Difficulty" && x.IsUnique);
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_BlueprintDetail_Quantity");
    }

    [Fact]
    public void Test_MatchesModeStatusAndFilteredIndexes()
    {
        var entity = Entity<TestEntity>("Test");

        AssertStringColumn(entity, nameof(TestEntity.TestId), "TestID", 36, nullable: false);
        AssertStringColumn(entity, nameof(TestEntity.BlueprintId), "BlueprintID", 36, nullable: true);
        AssertStringColumn(entity, nameof(TestEntity.TestStatus), "TestStatus", 20, nullable: false);
        AssertStringColumn(entity, nameof(TestEntity.TestMode), "TestMode", 30, nullable: false);
        AssertStringColumn(entity, nameof(TestEntity.TestCode), "TestCode", 20, nullable: true);
        Assert.Equal("Active", Column(entity, nameof(TestEntity.TestStatus)).GetDefaultValue());
        Assert.Equal("BlueprintExam", Column(entity, nameof(TestEntity.TestMode)).GetDefaultValue());
        Assert.Equal("datetime2(0)", Column(entity, nameof(TestEntity.CreatedTime)).GetColumnType());
        Assert.Equal(5, Column(entity, nameof(TestEntity.MaxScore)).GetPrecision());
        Assert.Equal(2, Column(entity, nameof(TestEntity.MaxScore)).GetScale());
        AssertStringColumn(entity, nameof(TestEntity.ScoringPolicy), "ScoringPolicy", 30, nullable: false);

        var testCodeIndex = Assert.Single(entity.GetIndexes(), x => x.GetDatabaseName() == "UX_Test_TestCode_NotNull");
        Assert.True(testCodeIndex.IsUnique);
        Assert.Equal("[TestCode] IS NOT NULL", testCodeIndex.GetFilter());
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_Test_Mode");
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_Test_Blueprint_Required");
        Assert.Contains(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.GetConstraintName() == "FK_Test_Student_GeneratedForStudentID" &&
                PropertyNames(foreignKey.Properties).SequenceEqual([nameof(TestEntity.GeneratedForStudentId)]));
    }

    [Fact]
    public void TestQuestion_MapsRecommendationAuditFields()
    {
        var entity = Entity<TestQuestion>("TestQuestion");

        AssertStringColumn(entity, nameof(TestQuestion.TestId), "TestID", 36, nullable: false);
        AssertStringColumn(entity, nameof(TestQuestion.QuestionId), "QuestionID", 36, nullable: false);
        AssertStringColumn(entity, nameof(TestQuestion.SourceBlueprintDetailId), "SourceBlueprintDetailID", 36, nullable: true);
        AssertStringColumn(entity, nameof(TestQuestion.SelectionReason), "SelectionReason", 40, nullable: false);
        AssertStringColumn(entity, nameof(TestQuestion.RecommendedForTagId), "RecommendedForTagID", 36, nullable: true);
        AssertStringColumn(entity, nameof(TestQuestion.RecommendedDifficultyId), "RecommendedDifficultyID", 36, nullable: true);
        AssertStringColumn(entity, nameof(TestQuestion.RuleVersion), "RuleVersion", 30, nullable: true);
        Assert.Equal(5, Column(entity, nameof(TestQuestion.PtagAtSelection)).GetPrecision());
        Assert.Equal(2, Column(entity, nameof(TestQuestion.PtagAtSelection)).GetScale());
        Assert.Equal("BlueprintNormal", Column(entity, nameof(TestQuestion.SelectionReason)).GetDefaultValue());
        AssertStringColumn(entity, nameof(TestQuestion.QuestionVersionId), "QuestionVersionID", 36, nullable: false);
        Assert.Equal(5, Column(entity, nameof(TestQuestion.WeightSnapshot)).GetPrecision());
        Assert.Equal(5, Column(entity, nameof(TestQuestion.MaxPointsSnapshot)).GetPrecision());
        AssertStringColumn(entity, nameof(TestQuestion.ScoringRuleSnapshot), "ScoringRuleSnapshot", 30, nullable: false);

        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() == "UQ_TestQuestion_Test_Order" && x.IsUnique);
        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() == "IX_TestQuestion_RecommendedTag_Difficulty");
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "CK_TestQuestion_SelectionReason");
        Assert.Contains(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.GetConstraintName() == "FK_TestQuestion_Question_QuestionID" &&
                PropertyNames(foreignKey.Properties).SequenceEqual([nameof(TestQuestion.QuestionId)]));
    }

    [Theory]
    [InlineData(typeof(AccountReadModel), "Account")]
    [InlineData(typeof(ExpertReadModel), "Expert")]
    [InlineData(typeof(TagTopicReadModel), "TagTopic")]
    [InlineData(typeof(TagDifficultyReadModel), "TagDifficulty")]
    [InlineData(typeof(StudentReadModel), "Student")]
    [InlineData(typeof(QuestionReadModel), "Question")]
    [InlineData(typeof(QuestionTopicReadModel), "QuestionTopic")]
    [InlineData(typeof(QuestionVersionReadModel), "QuestionVersion")]
    public void ExternalReadModels_AreExcludedFromMigrations(Type clrType, string tableName)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(clrType));

        Assert.Equal(tableName, entity.GetTableName());
        Assert.True(entity.IsTableExcludedFromMigrations());
    }

    [Fact]
    public void GenerationReadModels_MatchSqlKeysAndColumns()
    {
        var student = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(StudentReadModel)));
        AssertStringColumn(student, nameof(StudentReadModel.StudentId), "StudentID", 36, nullable: false);
        Assert.Equal("CurrentGrade", Column(student, nameof(StudentReadModel.CurrentGrade)).GetColumnName());

        var question = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(QuestionReadModel)));
        AssertStringColumn(question, nameof(QuestionReadModel.QuestionId), "QuestionID", 36, nullable: false);
        AssertStringColumn(question, nameof(QuestionReadModel.DifficultyId), "DifficultyID", 36, nullable: false);
        AssertStringColumn(question, nameof(QuestionReadModel.Status), "Status", 20, nullable: false);
        AssertStringColumn(question, nameof(QuestionReadModel.QuestionType), "QuestionType", 30, nullable: false);

        var topic = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(QuestionTopicReadModel)));
        AssertStringColumn(topic, nameof(QuestionTopicReadModel.QuestionTopicId), "QuestionTopicID", 36, nullable: false);
        AssertStringColumn(topic, nameof(QuestionTopicReadModel.QuestionId), "QuestionID", 36, nullable: false);
        AssertStringColumn(topic, nameof(QuestionTopicReadModel.TagId), "TagID", 36, nullable: false);
        Assert.Contains(topic.GetIndexes(), x => x.GetDatabaseName() == "UQ_QuestionTopic_Question_Tag" && x.IsUnique);

        var version = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(QuestionVersionReadModel)));
        AssertStringColumn(version, nameof(QuestionVersionReadModel.VersionId), "VersionID", 36, nullable: false);
        Assert.Equal("AnswersSnapshot", Column(version, nameof(QuestionVersionReadModel.AnswersSnapshot)).GetColumnName());
    }

    private IEntityType Entity<TEntity>(string tableName)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));
        Assert.Equal(tableName, entity.GetTableName());
        Assert.False(entity.IsTableExcludedFromMigrations());
        return entity;
    }

    private static IProperty Column(IEntityType entity, string propertyName)
        => Assert.IsAssignableFrom<IProperty>(entity.FindProperty(propertyName));

    private static void AssertStringColumn(
        IEntityType entity,
        string propertyName,
        string columnName,
        int maxLength,
        bool nullable,
        bool unicode = false)
    {
        var property = Column(entity, propertyName);
        Assert.Equal(typeof(string), property.ClrType);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.Equal(unicode, property.IsUnicode());
        Assert.Equal(nullable, property.IsNullable);
    }

    private static IEnumerable<string> PropertyNames(IEnumerable<IProperty> properties)
        => properties.Select(x => x.Name);
}

using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests;

public sealed class RecommenderModelMetadataTests
{
    private readonly IModel _model;

    public RecommenderModelMetadataTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseSqlServer("Server=localhost;Database=MathInsightMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new RecommenderDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void OwnedTables_MatchCanonicalSqlColumns()
    {
        var competency = Entity<CompetencyPoint>("CompetencyPoint", excluded: false);
        AssertString(competency, nameof(CompetencyPoint.CompetencyId), "CompetencyID");
        AssertString(competency, nameof(CompetencyPoint.StudentId), "StudentID");
        AssertColumn(competency, nameof(CompetencyPoint.Grade), "Grade");
        AssertPrecision(competency, nameof(CompetencyPoint.Point), "Point", 5, 2);
        AssertPrimaryKey(competency, "PK_CompetencyPoint", nameof(CompetencyPoint.CompetencyId));
        AssertIndex(
            competency,
            "UQ_CompetencyPoint_Student_Grade",
            unique: true,
            nameof(CompetencyPoint.StudentId),
            nameof(CompetencyPoint.Grade));
        AssertCheckConstraints(
            competency,
            "CK_CompetencyPoint_Grade",
            "CK_CompetencyPoint_Point");

        var mastery = Entity<TagsMastery>("TagsMastery", excluded: false);
        AssertString(mastery, nameof(TagsMastery.TagsMasteryId), "TagsMasteryID");
        AssertString(mastery, nameof(TagsMastery.StudentId), "StudentID");
        AssertString(mastery, nameof(TagsMastery.TagId), "TagID");
        AssertPrecision(mastery, nameof(TagsMastery.OfficialPoint), "OfficialPoint", 5, 2);
        AssertPrecision(mastery, nameof(TagsMastery.PracticePoint), "PracticePoint", 5, 2);
        AssertPrecision(mastery, nameof(TagsMastery.ExamAnchor), "ExamAnchor", 5, 2);
        AssertPrecision(mastery, nameof(TagsMastery.AccuracyRate), "AccuracyRate", 5, 2);
        AssertColumns(
            mastery,
            (nameof(TagsMastery.ExamHistory), "ExamHistory"),
            (nameof(TagsMastery.SeriesAnswerCount), "SeriesAnswerCount"),
            (nameof(TagsMastery.RecommendedDifficultyLevel), "RecommendedDifficultyLevel"),
            (nameof(TagsMastery.MasteryStatus), "MasteryStatus"),
            (nameof(TagsMastery.NumberDone), "NumberDone"),
            (nameof(TagsMastery.NumCorrect), "NumCorrect"),
            (nameof(TagsMastery.LastCalculatedAt), "LastCalculatedAt"),
            (nameof(TagsMastery.LastPracticedTime), "LastPracticedTime"));
        AssertPrimaryKey(mastery, "PK_TagsMastery", nameof(TagsMastery.TagsMasteryId));
        AssertIndex(
            mastery,
            "UQ_TagsMastery_Student_Tag",
            unique: true,
            nameof(TagsMastery.StudentId),
            nameof(TagsMastery.TagId));
        AssertIndex(
            mastery,
            "IX_TagsMastery_Student_OfficialPoint",
            unique: false,
            nameof(TagsMastery.StudentId),
            nameof(TagsMastery.OfficialPoint),
            nameof(TagsMastery.TagId));
        AssertCheckConstraints(
            mastery,
            "CK_TagsMastery_ExamHistoryJson",
            "CK_TagsMastery_Points",
            "CK_TagsMastery_Progress",
            "CK_TagsMastery_RecommendedDifficultyLevel",
            "CK_TagsMastery_SeriesAnswerCount",
            "CK_TagsMastery_Status");

        var snapshot = Entity<StudentTopicSessionResult>("StudentTopicSessionResult", excluded: false);
        AssertString(snapshot, nameof(StudentTopicSessionResult.StudentTopicSessionResultId), "StudentTopicSessionResultID");
        AssertString(snapshot, nameof(StudentTopicSessionResult.StudentId), "StudentID");
        AssertString(snapshot, nameof(StudentTopicSessionResult.SessionId), "SessionID");
        AssertString(snapshot, nameof(StudentTopicSessionResult.TagId), "TagID");
        AssertPrecision(snapshot, nameof(StudentTopicSessionResult.TotalItems), "TotalItems", 6, 2);
        AssertPrecision(snapshot, nameof(StudentTopicSessionResult.CorrectItems), "CorrectItems", 6, 2);
        AssertPrecision(snapshot, nameof(StudentTopicSessionResult.EarnedPoints), "EarnedPoints", 6, 2);
        AssertPrecision(snapshot, nameof(StudentTopicSessionResult.MaxPoints), "MaxPoints", 6, 2);
        AssertPrecision(snapshot, nameof(StudentTopicSessionResult.TopicScore), "TopicScore", 5, 2);
        AssertColumn(snapshot, nameof(StudentTopicSessionResult.CreatedTime), "CreatedTime");
        AssertPrimaryKey(
            snapshot,
            "PK_StudentTopicSessionResult",
            nameof(StudentTopicSessionResult.StudentTopicSessionResultId));
        AssertIndex(
            snapshot,
            "UQ_StudentTopicSessionResult_Session_Tag",
            unique: true,
            nameof(StudentTopicSessionResult.SessionId),
            nameof(StudentTopicSessionResult.TagId));
        AssertIndex(
            snapshot,
            "IX_StudentTopicSessionResult_Student_Tag_Created",
            unique: false,
            nameof(StudentTopicSessionResult.StudentId),
            nameof(StudentTopicSessionResult.TagId),
            nameof(StudentTopicSessionResult.CreatedTime));
        AssertIndex(
            snapshot,
            "IX_StudentTopicSessionResult_SessionID",
            unique: false,
            nameof(StudentTopicSessionResult.SessionId));
        AssertCheckConstraints(snapshot, "CK_StudentTopicSessionResult_Values");
        Assert.Null(snapshot.FindProperty("WrongCount"));
        Assert.Null(snapshot.FindProperty("PointBefore"));
        Assert.Null(snapshot.FindProperty("PointAfter"));
    }

    [Fact]
    public void ExternalReadModels_MatchCanonicalSqlAndAreExcludedFromMigrations()
    {
        var tag = Entity<TagTopicReadOnly>("TagTopic", excluded: true);
        AssertString(tag, nameof(TagTopicReadOnly.TagId), "TagID");
        AssertColumns(
            tag,
            (nameof(TagTopicReadOnly.TagName), "TagName"),
            (nameof(TagTopicReadOnly.Grade), "Grade"),
            (nameof(TagTopicReadOnly.IsActive), "IsActive"));

        var student = Entity<StudentReadOnly>("Student", excluded: true);
        AssertString(student, nameof(StudentReadOnly.StudentId), "StudentID");
        AssertColumn(student, nameof(StudentReadOnly.CurrentGrade), "CurrentGrade");

        var lecture = Entity<LectureReadOnly>("Lecture", excluded: true);
        AssertString(lecture, nameof(LectureReadOnly.LectureId), "LectureID");
        AssertString(lecture, nameof(LectureReadOnly.TagId), "TagID");
        AssertColumns(
            lecture,
            (nameof(LectureReadOnly.Title), "Title"),
            (nameof(LectureReadOnly.Content), "Content"),
            (nameof(LectureReadOnly.Status), "Status"));
        Assert.Null(lecture.FindProperty("Description"));

        var material = Entity<MaterialReadOnly>("Material", excluded: true);
        AssertString(material, nameof(MaterialReadOnly.MaterialId), "MaterialID");
        AssertColumns(
            material,
            (nameof(MaterialReadOnly.MaterialName), "MaterialName"),
            (nameof(MaterialReadOnly.FileUrl), "FileUrl"),
            (nameof(MaterialReadOnly.FileType), "FileType"),
            (nameof(MaterialReadOnly.Status), "Status"));
        Assert.Null(material.FindProperty("Description"));

        var link = Entity<LectureMaterialReadOnly>("LectureMaterial", excluded: true);
        AssertString(link, nameof(LectureMaterialReadOnly.LectureId), "LectureID");
        AssertString(link, nameof(LectureMaterialReadOnly.MaterialId), "MaterialID");
        Assert.Equal(
            [nameof(LectureMaterialReadOnly.LectureId), nameof(LectureMaterialReadOnly.MaterialId)],
            link.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Null(link.FindProperty("LectureMaterialId"));
    }

    private IEntityType Entity<TEntity>(string tableName, bool excluded)
    {
        var entity = Assert.IsAssignableFrom<IEntityType>(_model.FindEntityType(typeof(TEntity)));
        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(excluded, entity.IsTableExcludedFromMigrations());
        return entity;
    }

    private static IProperty Property(IEntityType entity, string name)
        => Assert.IsAssignableFrom<IProperty>(entity.FindProperty(name));

    private static void AssertColumn(IEntityType entity, string propertyName, string columnName)
        => Assert.Equal(columnName, Property(entity, propertyName).GetColumnName());

    private static void AssertColumns(
        IEntityType entity,
        params (string PropertyName, string ColumnName)[] columns)
    {
        foreach (var (propertyName, columnName) in columns)
            AssertColumn(entity, propertyName, columnName);
    }

    private static void AssertString(IEntityType entity, string propertyName, string columnName)
    {
        var property = Property(entity, propertyName);
        Assert.Equal(typeof(string), property.ClrType);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(36, property.GetMaxLength());
        Assert.False(property.IsUnicode());
        Assert.Equal("varchar(36)", property.GetRelationalTypeMapping().StoreType.ToLowerInvariant());
    }

    private static void AssertPrecision(
        IEntityType entity,
        string propertyName,
        string columnName,
        int precision,
        int scale)
    {
        var property = Property(entity, propertyName);
        Assert.Equal(columnName, property.GetColumnName());
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }

    private static void AssertPrimaryKey(
        IEntityType entity,
        string databaseName,
        params string[] propertyNames)
    {
        var primaryKey = Assert.IsAssignableFrom<IKey>(entity.FindPrimaryKey());
        Assert.Equal(databaseName, primaryKey.GetName());
        Assert.Equal(propertyNames, primaryKey.Properties.Select(property => property.Name));
    }

    private static void AssertIndex(
        IEntityType entity,
        string databaseName,
        bool unique,
        params string[] propertyNames)
    {
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        Assert.Equal(databaseName, index.GetDatabaseName());
        Assert.Equal(unique, index.IsUnique);
    }

    private static void AssertCheckConstraints(IEntityType entity, params string[] expectedNames)
    {
        var actualNames = entity.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .Order()
            .ToArray();
        Assert.Equal(expectedNames.Order().ToArray(), actualNames);
    }
}

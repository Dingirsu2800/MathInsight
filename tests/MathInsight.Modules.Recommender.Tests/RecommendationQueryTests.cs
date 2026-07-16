using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;
using MathInsight.Modules.Recommender.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests;

public sealed class RecommendationQueryTests : IDisposable
{
    private readonly RecommenderDbContext _db;

    public RecommendationQueryTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D"))
            .Options;
        _db = new RecommenderDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Queries_ReturnOnlyPublishedLecturesAndActiveMaterials()
    {
        const string studentId = "student_01";
        const string tagId = "TOPIC-G12-DERIVAPP";
        _db.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = tagId,
            TagName = "Derivative applications",
            Grade = 12,
            IsActive = true
        });
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = "mastery_01",
            StudentId = studentId,
            TagId = tagId,
            OfficialPoint = 2m,
            PracticePoint = 2m,
            ExamAnchor = 2m,
            RecommendedDifficultyLevel = 1,
            MasteryStatus = "Learning"
        });
        _db.Lectures.AddRange(
            Lecture("lecture_published", tagId, "Published"),
            Lecture("lecture_draft", tagId, "Draft"));
        _db.Materials.AddRange(
            Material("material_active", "Active"),
            Material("material_inactive", "Deactivated"));
        _db.LectureMaterials.AddRange(
            new LectureMaterialReadOnly { LectureId = "lecture_published", MaterialId = "material_active" },
            new LectureMaterialReadOnly { LectureId = "lecture_published", MaterialId = "material_inactive" });
        await _db.SaveChangesAsync();

        var difficulty = new DifficultyMappingService();
        var lectures = await new GetRecommendedLecturesQueryHandler(_db, difficulty)
            .Handle(new GetRecommendedLecturesQuery(studentId), default);
        var materials = await new GetRecommendedMaterialsQueryHandler(_db, difficulty)
            .Handle(new GetRecommendedMaterialsQuery(studentId), default);

        var lecture = Assert.Single(lectures);
        Assert.Equal("lecture_published", lecture.LectureId);
        Assert.Equal("Lecture content", lecture.Description);
        var material = Assert.Single(materials);
        Assert.Equal("material_active", material.MaterialId);
        Assert.Equal("Active material", material.Title);
        Assert.Equal("pdf", material.MaterialType);
        Assert.Null(material.Description);
    }

    private static LectureReadOnly Lecture(string id, string tagId, string status)
        => new()
        {
            LectureId = id,
            Title = id,
            Content = "Lecture content",
            TagId = tagId,
            Status = status
        };

    private static MaterialReadOnly Material(string id, string status)
        => new()
        {
            MaterialId = id,
            MaterialName = status == "Active" ? "Active material" : "Inactive material",
            FileUrl = "https://example.test/material.pdf",
            FileType = "pdf",
            Status = status
        };
}

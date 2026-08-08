using System;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Commands.Lectures;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Modules.Learning_Lecture.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class LectureCommandTests
{
    [Fact]
    public async Task CreateLecture_AlwaysSetsStatusToDraft()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTaxonomy(database.Context);
        await database.Context.SaveChangesAsync();
        var handler = new CreateLectureCommandHandler(database.Context);
        var command = new CreateLectureCommand("Test Lecture", "Content", null, null, "tag-1", "difficulty-1", "teacher-1", null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Draft", result.Value!.Status);
        
        var lectureInDb = await database.Context.Lectures.FirstOrDefaultAsync(l => l.LectureId == result.Value.LectureId);
        Assert.NotNull(lectureInDb);
        Assert.Equal("Draft", lectureInDb.Status);
        Assert.Equal("teacher-1", lectureInDb.TeacherId);
    }

    [Fact]
    public async Task PublishLecture_ValidRequest_UpdatesStatusToPublished()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test",
            Content = "Valid content",
            TagId = "tag-1",
            DifficultyId = "difficulty-1",
            TeacherId = "teacher-1",
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        AddDifficulty(database.Context);
        await database.Context.SaveChangesAsync();

        var handler = new PublishLectureCommandHandler(database.Context);
        var command = new PublishLectureCommand(lectureId, "teacher-1", false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        var updatedLecture = await database.Context.Lectures.FirstOrDefaultAsync(l => l.LectureId == lectureId);
        Assert.Equal("Published", updatedLecture!.Status);
    }

    [Fact]
    public async Task PublishLecture_NoContentOrVideoUrl_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test",
            Content = null, // Invalid
            VideoUrl = null, // Invalid
            TagId = "tag-1",
            TeacherId = "teacher-1",
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new PublishLectureCommandHandler(database.Context);
        var command = new PublishLectureCommand(lectureId, "teacher-1", false);

        // Act & Assert
        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureContentRequired, result.Error);
    }

    [Fact]
    public async Task PublishLecture_NotOwner_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test",
            Content = "Content",
            TagId = "tag-1",
            TeacherId = "teacher-1", // Owner is teacher-1
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new PublishLectureCommandHandler(database.Context);
        var command = new PublishLectureCommand(lectureId, "teacher-2", false); // Caller is teacher-2

        // Act & Assert
        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureForbidden, result.Error);
    }

    private static void AddActiveTaxonomy(LearningDbContext context)
    {
        context.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = "tag-1",
            TagName = "Topic 1",
            Grade = 12,
            IsActive = true,
            DisplayOrder = 1
        });
        AddDifficulty(context);
    }

    private static void AddDifficulty(LearningDbContext context)
    {
        context.TagDifficulties.Add(new TagDifficultyReadOnly
        {
            DifficultyId = "difficulty-1",
            DifficultyName = "Difficulty 1",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        });
    }
}

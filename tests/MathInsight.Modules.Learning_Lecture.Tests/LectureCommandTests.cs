using System;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Commands.Lectures;
using MathInsight.Modules.Learning_Lecture.Entities;
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
        var handler = new CreateLectureCommandHandler(database.Context);
        var command = new CreateLectureCommand("Test Lecture", "Content", null, null, "tag-1", "teacher-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        
        var lectureInDb = await database.Context.Lectures.FirstOrDefaultAsync(l => l.LectureId == result.LectureId);
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
            TeacherId = "teacher-1",
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new PublishLectureCommandHandler(database.Context);
        var command = new PublishLectureCommand(lectureId, "teacher-1", false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
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
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("must have either VideoUrl or Content", ex.Message);
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
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Forbidden", ex.Message);
    }
}

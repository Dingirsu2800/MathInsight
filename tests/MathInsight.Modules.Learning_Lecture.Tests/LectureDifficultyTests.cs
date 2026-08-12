using MathInsight.Modules.Learning_Lecture.Commands.Lectures;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Queries.Difficulties;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class LectureDifficultyTests
{
    [Fact]
    public async Task CreateLecture_BlankDifficulty_ReturnsRequiredError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTopic(database.Context);
        await database.Context.SaveChangesAsync();

        var result = await new CreateLectureCommandHandler(database.Context).Handle(
            new CreateLectureCommand("Lecture", "Content", null, null, "topic-1", "", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyRequired, result.Error);
    }

    [Fact]
    public async Task CreateLecture_UnknownDifficulty_ReturnsNotFoundError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTopic(database.Context);
        await database.Context.SaveChangesAsync();

        var result = await new CreateLectureCommandHandler(database.Context).Handle(
            new CreateLectureCommand("Lecture", "Content", null, null, "topic-1", "unknown", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyNotFound, result.Error);
    }

    [Fact]
    public async Task CreateLecture_InactiveDifficulty_ReturnsInactiveError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTopic(database.Context);
        AddDifficulty(database.Context, "difficulty-1", isActive: false);
        await database.Context.SaveChangesAsync();

        var result = await new CreateLectureCommandHandler(database.Context).Handle(
            new CreateLectureCommand("Lecture", "Content", null, null, "topic-1", "difficulty-1", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyInactive, result.Error);
    }

    [Fact]
    public async Task CreateLecture_InactiveTopic_ReturnsInactiveError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTopic(database.Context, isActive: false);
        AddDifficulty(database.Context, "difficulty-1");
        await database.Context.SaveChangesAsync();

        var result = await new CreateLectureCommandHandler(database.Context).Handle(
            new CreateLectureCommand("Lecture", "Content", null, null, "topic-1", "difficulty-1", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureTopicInactive, result.Error);
    }

    [Fact]
    public async Task CreateLecture_RootTopic_ReturnsTopicMustBeLeafError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        database.Context.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = "root-topic",
            TagName = "Root topic",
            Grade = 12,
            IsActive = true,
            DisplayOrder = 1
        });
        AddDifficulty(database.Context, "difficulty-1");
        await database.Context.SaveChangesAsync();

        var result = await new CreateLectureCommandHandler(database.Context).Handle(
            new CreateLectureCommand("Lecture", "Content", null, null, "root-topic", "difficulty-1", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureTopicMustBeLeaf, result.Error);
    }

    [Fact]
    public async Task UpdateLecture_ActiveDifficulty_UpdatesDifficultyId()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddActiveTopic(database.Context);
        AddDifficulty(database.Context, "difficulty-1", levelValue: 1);
        AddDifficulty(database.Context, "difficulty-2", levelValue: 2);
        database.Context.Lectures.Add(NewLecture("lecture-1", "topic-1", "difficulty-1"));
        await database.Context.SaveChangesAsync();

        var result = await new UpdateLectureCommandHandler(database.Context).Handle(
            new UpdateLectureCommand("lecture-1", "Updated", "Content", null, null, "topic-1", "difficulty-2", "teacher-1", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("difficulty-2", (await database.Context.Lectures.SingleAsync()).DifficultyId);
    }

    [Fact]
    public async Task PublishLecture_NullDifficulty_ReturnsRequiredError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        database.Context.Lectures.Add(NewLecture("lecture-1", "topic-1", difficultyId: null));
        await database.Context.SaveChangesAsync();

        var result = await new PublishLectureCommandHandler(database.Context).Handle(
            new PublishLectureCommand("lecture-1", "teacher-1", false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyRequired, result.Error);
    }

    [Fact]
    public async Task PublishLecture_UnknownDifficulty_ReturnsNotFoundError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        database.Context.Lectures.Add(NewLecture("lecture-1", "topic-1", "missing"));
        await database.Context.SaveChangesAsync();

        var result = await new PublishLectureCommandHandler(database.Context).Handle(
            new PublishLectureCommand("lecture-1", "teacher-1", false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyNotFound, result.Error);
    }

    [Fact]
    public async Task PublishLecture_InactiveDifficulty_ReturnsInactiveError()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        database.Context.Lectures.Add(NewLecture("lecture-1", "topic-1", "difficulty-1"));
        AddDifficulty(database.Context, "difficulty-1", isActive: false);
        await database.Context.SaveChangesAsync();

        var result = await new PublishLectureCommandHandler(database.Context).Handle(
            new PublishLectureCommand("lecture-1", "teacher-1", false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LearningErrors.LectureDifficultyInactive, result.Error);
    }

    [Fact]
    public async Task GetDifficultyList_ReturnsOnlyActiveRowsInStableOrder()
    {
        await using var database = await LearningInMemoryContext.CreateAsync();
        AddDifficulty(database.Context, "difficulty-b", levelValue: 2, displayOrder: 20);
        AddDifficulty(database.Context, "difficulty-c", levelValue: 3, displayOrder: 10);
        AddDifficulty(database.Context, "difficulty-a", levelValue: 1, displayOrder: 10);
        AddDifficulty(database.Context, "difficulty-inactive", isActive: false);
        await database.Context.SaveChangesAsync();

        var result = await new GetDifficultyListQueryHandler(database.Context).Handle(
            new GetDifficultyListQuery(),
            CancellationToken.None);

        Assert.Equal(new[] { "difficulty-a", "difficulty-c", "difficulty-b" }, result.Select(x => x.DifficultyId));
    }

    private static void AddActiveTopic(LearningDbContext context, bool isActive = true)
    {
        context.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = "root-topic-1",
            TagName = "Root topic",
            Grade = 12,
            IsActive = true,
            DisplayOrder = 1
        });
        context.TagTopics.Add(new TagTopicReadOnly
        {
            TagId = "topic-1",
            ParentTagId = "root-topic-1",
            TagName = "Topic 1",
            Grade = 12,
            IsActive = isActive,
            DisplayOrder = 1
        });
    }

    private static void AddDifficulty(
        LearningDbContext context,
        string difficultyId,
        bool isActive = true,
        int levelValue = 1,
        int displayOrder = 1)
    {
        context.TagDifficulties.Add(new TagDifficultyReadOnly
        {
            DifficultyId = difficultyId,
            DifficultyName = difficultyId,
            LevelValue = levelValue,
            DisplayOrder = displayOrder,
            IsActive = isActive
        });
    }

    private static Lecture NewLecture(string lectureId, string tagId, string? difficultyId) => new()
    {
        LectureId = lectureId,
        Title = "Lecture",
        Content = "Content",
        TagId = tagId,
        DifficultyId = difficultyId,
        TeacherId = "teacher-1",
        Status = "Draft",
        CreatedTime = DateTime.UtcNow,
        UpdatedTime = DateTime.UtcNow
    };
}

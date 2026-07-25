using MathInsight.Modules.QuestionBank.Commands.DeleteTagTopic;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;
using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;
using MathInsight.Modules.QuestionBank.Queries.GetTagTopics;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class TagQueryAndDeactivationTests
{
    [Fact]
    public async Task TopicQuery_ExcludesInactiveTopicsByDefault_AndIncludesThemWhenRequested()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var parent = CreateTopic("topic-parent", null, 10, true);
        var inactiveChild = CreateTopic("topic-child", parent.TagId, 10, false);
        inactiveChild.ParentTag = parent;

        database.Context.TagTopics.AddRange(parent, inactiveChild);
        await database.Context.SaveChangesAsync();

        var handler = new GetTagTopicTreeQueryHandler(database.Context);

        var activeOnly = await handler.Handle(new GetTagTopicTreeQuery(null), CancellationToken.None);
        var includingInactive = await handler.Handle(new GetTagTopicTreeQuery(null, true), CancellationToken.None);

        var activeParent = Assert.Single(activeOnly);
        Assert.Empty(activeParent.Children);

        var completeParent = Assert.Single(includingInactive);
        var returnedChild = Assert.Single(completeParent.Children);
        Assert.False(returnedChild.IsActive);
    }

    [Fact]
    public async Task TopicQuery_AppliesGradeFilterTogetherWithIncludeInactive()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        database.Context.TagTopics.AddRange(
            CreateTopic("grade-10", null, 10, true),
            CreateTopic("grade-11", null, 11, false));
        await database.Context.SaveChangesAsync();

        var handler = new GetTagTopicTreeQueryHandler(database.Context);
        var result = await handler.Handle(new GetTagTopicTreeQuery(11, true), CancellationToken.None);

        var topic = Assert.Single(result);
        Assert.Equal("grade-11", topic.TagId);
        Assert.False(topic.IsActive);
    }

    [Fact]
    public async Task DifficultyQuery_ExcludesInactiveDifficultiesByDefault_AndIncludesThemWhenRequested()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        database.Context.TagDifficulties.AddRange(
            new TagDifficulty { DifficultyId = "easy", DifficultyName = "Easy", LevelValue = 1, DisplayOrder = 1, IsActive = true },
            new TagDifficulty { DifficultyId = "hard", DifficultyName = "Hard", LevelValue = 2, DisplayOrder = 2, IsActive = false });
        await database.Context.SaveChangesAsync();

        var handler = new GetTagDifficultiesQueryHandler(database.Context);

        var activeOnly = await handler.Handle(new GetTagDifficultiesQuery(), CancellationToken.None);
        var includingInactive = await handler.Handle(new GetTagDifficultiesQuery(true), CancellationToken.None);

        Assert.Single(activeOnly);
        Assert.Equal(2, includingInactive.Count);
        Assert.Contains(includingInactive, difficulty => difficulty.DifficultyId == "hard" && !difficulty.IsActive);
    }

    [Fact]
    public async Task UpdateTopic_WhenAnActiveGrandchildExists_ReturnsConflictErrorWithoutMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var parent = CreateTopic("parent", null, 10, true);
        var child = CreateTopic("child", parent.TagId, 10, false);
        var grandchild = CreateTopic("grandchild", child.TagId, 10, true);
        child.ParentTag = parent;
        grandchild.ParentTag = child;

        database.Context.TagTopics.AddRange(parent, child, grandchild);
        await database.Context.SaveChangesAsync();

        var request = new UpdateTagTopicRequest
        {
            ParentTagId = null,
            TagName = parent.TagName,
            Grade = parent.Grade,
            DisplayOrder = parent.DisplayOrder,
            IsActive = false
        };

        var result = await new UpdateTagTopicCommandHandler(database.Context)
            .Handle(new UpdateTagTopicCommand(parent.TagId, request), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagTopicHasActiveDescendants, result.Error);
        Assert.True(parent.IsActive);
    }

    [Fact]
    public async Task DeleteTopic_WhenAnActiveDescendantExists_ReturnsConflictErrorWithoutMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var parent = CreateTopic("parent", null, 10, true);
        var child = CreateTopic("child", parent.TagId, 10, true);
        child.ParentTag = parent;

        database.Context.TagTopics.AddRange(parent, child);
        await database.Context.SaveChangesAsync();

        var result = await new DeleteTagTopicCommandHandler(database.Context)
            .Handle(new DeleteTagTopicCommand(parent.TagId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagTopicHasActiveDescendants, result.Error);
        Assert.True(parent.IsActive);
    }

    [Fact]
    public async Task UpdateTopic_CanDeactivateLeafTopic()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var leaf = CreateTopic("leaf", null, 12, true);
        database.Context.TagTopics.Add(leaf);
        await database.Context.SaveChangesAsync();

        var request = new UpdateTagTopicRequest
        {
            ParentTagId = null,
            TagName = leaf.TagName,
            Grade = leaf.Grade,
            DisplayOrder = leaf.DisplayOrder,
            IsActive = false
        };

        var result = await new UpdateTagTopicCommandHandler(database.Context)
            .Handle(new UpdateTagTopicCommand(leaf.TagId, request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(leaf.IsActive);
    }

    private static TagTopic CreateTopic(string tagId, string? parentTagId, int grade, bool isActive)
    {
        return new TagTopic
        {
            TagId = tagId,
            ParentTagId = parentTagId,
            TagName = tagId,
            Grade = grade,
            DisplayOrder = 1,
            IsActive = isActive
        };
    }
}

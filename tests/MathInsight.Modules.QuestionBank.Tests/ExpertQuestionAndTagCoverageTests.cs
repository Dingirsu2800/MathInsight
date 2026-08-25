using MathInsight.Modules.QuestionBank.Commands.CreateQuestion;
using MathInsight.Modules.QuestionBank.Commands.CreateTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.CreateTagTopic;
using MathInsight.Modules.QuestionBank.Commands.DeleteTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.DeleteTagTopic;
using MathInsight.Modules.QuestionBank.Commands.RetryScoreAdjustment;
using MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagDifficulty;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;
using MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;
using MathInsight.Modules.QuestionBank.Queries.GetTagTopics;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class ExpertQuestionAndTagCoverageTests
{
    [Fact]
    public async Task CreateQuestion_WhenDifficultyDoesNotExist_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "topic-1", 10);

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("missing-difficulty", "topic-1"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionDifficultyNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
        Assert.Empty(await database.Context.QuestionVersions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicDoesNotExist_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "missing-topic"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
        Assert.Empty(await database.Context.QuestionVersions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenDifficultyOrTopicIsInactive_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-active", 1);
        await AddDifficultyAsync(database, "difficulty-inactive", 1, isActive: false);
        await AddTopicAsync(database, "topic-active", 10);
        await AddTopicAsync(database, "topic-inactive", 10, isActive: false);

        var inactiveDifficulty = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-inactive", "topic-active"), "expert-1"), CancellationToken.None);
        var activeDifficultyInactiveTopic = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-active", "topic-inactive"), "expert-1"), CancellationToken.None);

        Assert.True(inactiveDifficulty.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionDifficultyNotFound, inactiveDifficulty.Error);
        Assert.True(activeDifficultyInactiveTopic.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, activeDifficultyInactiveTopic.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicGradeDiffers_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-grade-11", 11);

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-grade-11"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicIsRootGrouping_RejectsAssignment()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "root-topic", 10, isRoot: true);

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "root-topic"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicMustBeDirectChild, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicHasInactiveAncestor_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "inactive-parent", 10, isActive: false);
        await AddTopicAsync(database, "active-child", 10, parentTagId: "inactive-parent");

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "active-child"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicHasMissingAncestor_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "orphan-topic", 10, parentTagId: "missing-parent");

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "orphan-topic"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task CreateQuestion_WhenTopicHierarchyIsCyclic_ReturnsNotFoundAndWritesNothing()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-a", 10);
        await AddTopicAsync(database, "topic-b", 10, parentTagId: "topic-a");
        var topicA = await database.Context.TagTopics.SingleAsync(topic => topic.TagId == "topic-a");
        topicA.ParentTagId = "topic-b";
        await database.Context.SaveChangesAsync();

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-a"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task UpdateQuestion_WhenTopicWasMovedUnderInactiveAncestor_RejectsRequestAndPreservesExistingQuestion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "parent", 10, isRoot: true);
        await AddTopicAsync(database, "child", 10, parentTagId: "parent");
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "child"), "expert-1"), CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        var parent = await database.Context.TagTopics.SingleAsync(topic => topic.TagId == "parent");
        parent.IsActive = false;
        await database.Context.SaveChangesAsync();

        var request = ToUpdateQuestionRequest(CreateQuestionRequest("difficulty-1", "child"));
        request.QuestionContent = "This update must not be persisted";
        var result = await new UpdateQuestionCommandHandler(database.Context)
            .Handle(new UpdateQuestionCommand(createResult.Value!.QuestionId, request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTopicNotFound, result.Error);
        var persisted = Assert.Single(await database.Context.Questions.ToListAsync());
        Assert.Equal("What is 2 + 2?", persisted.QuestionContent);
    }

    [Fact]
    public async Task UpdateQuestion_WhenDifficultyWasSoftDeleted_RejectsRequestAndPreservesExistingQuestion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        await new DeleteTagDifficultyCommandHandler(database.Context)
            .Handle(new DeleteTagDifficultyCommand("difficulty-1"), CancellationToken.None);

        var request = ToUpdateQuestionRequest(CreateQuestionRequest("difficulty-1", "topic-1"));
        request.QuestionContent = "This update must not be persisted";
        var result = await new UpdateQuestionCommandHandler(database.Context)
            .Handle(new UpdateQuestionCommand(createResult.Value!.QuestionId, request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionDifficultyNotFound, result.Error);
        var persisted = Assert.Single(await database.Context.Questions.ToListAsync());
        Assert.Equal("What is 2 + 2?", persisted.QuestionContent);
    }

    [Fact]
    public async Task CreateQuestion_WithKnownReferences_PersistsApprovedQuestionAndVersionOne()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(await database.Context.Questions.Include(item => item.Answers).ToListAsync());
        Assert.Equal("Approved", question.Status);
        Assert.True(question.IsActive);
        Assert.Equal("expert-1", question.ExpertId);
        Assert.Equal(2, question.Answers.Count);
        var version = Assert.Single(await database.Context.QuestionVersions.ToListAsync());
        Assert.Equal(question.QuestionId, version.QuestionId);
        Assert.Equal(1, version.VersionNumber);
    }

    [Fact]
    public async Task CreateQuestion_MultipleChoice_PersistsAllAnswersAndMappedType()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "MULTIPLE_CHOICE";
        request.Answers =
        [
            new CreateAnswerRequest { AnswerContent = "A", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "B", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "C", IsCorrect = false }
        ];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(await database.Context.Questions.Include(item => item.Answers).ToListAsync());
        Assert.Equal("MultipleChoice", question.QuestionType);
        Assert.Equal(3, question.Answers.Count);
        Assert.Equal(2, question.Answers.Count(answer => answer.IsCorrect));
    }

    [Fact]
    public async Task CreateQuestion_Composite_PersistsPartsWithoutTopLevelAnswers()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "COMPOSITE";
        request.Answers = [];
        request.Parts =
        [
            new CreateQuestionPartRequest
            {
                PartOrder = 1,
                PartLabel = "a",
                PartContent = "Calculate 2 + 2",
                PartType = "NUMERIC_ANSWER",
                CorrectNumeric = 4m,
                NumericTolerance = 0m,
                DefaultWeight = 1m
            }
        ];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(await database.Context.Questions
            .Include(item => item.Parts)
            .Include(item => item.Answers)
            .ToListAsync());
        Assert.Equal("Composite", question.QuestionType);
        Assert.Empty(question.Answers);
        var part = Assert.Single(question.Parts);
        Assert.Equal("NumericAnswer", part.PartType);
        Assert.Equal(4m, part.CorrectNumeric);
    }

    [Fact]
    public async Task CreateQuestion_TrueFalseWithWrongAnswerCount_ReturnsValidationErrorWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "TRUE_FALSE";
        request.Answers = [new CreateAnswerRequest { AnswerContent = "True", IsCorrect = true }];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionTrueFalseAnswerCountInvalid, result.Error);
        Assert.Empty(await database.Context.Questions.ToListAsync());
    }

    [Fact]
    public async Task UpdateQuestion_ByAnotherExpert_ReturnsForbiddenWithoutNewVersion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        var request = ToUpdateQuestionRequest(CreateQuestionRequest("difficulty-1", "topic-1"));
        request.QuestionContent = "Unauthorized update";
        var result = await new UpdateQuestionCommandHandler(database.Context)
            .Handle(new UpdateQuestionCommand(createResult.Value!.QuestionId, request, "expert-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionUpdateForbidden, result.Error);
        var question = Assert.Single(await database.Context.Questions.ToListAsync());
        Assert.Equal("What is 2 + 2?", question.QuestionContent);
        var version = Assert.Single(await database.Context.QuestionVersions.ToListAsync());
        Assert.Equal(1, version.VersionNumber);
    }

    [Fact]
    public async Task ToggleQuestionActive_ByOwner_DeactivatesThenReactivatesQuestion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        var handler = new ToggleQuestionActiveCommandHandler(database.Context);
        var deactivated = await handler.Handle(
            new ToggleQuestionActiveCommand(createResult.Value!.QuestionId, false, "expert-1"),
            CancellationToken.None);
        var reactivated = await handler.Handle(
            new ToggleQuestionActiveCommand(createResult.Value.QuestionId, true, "expert-1"),
            CancellationToken.None);

        Assert.True(deactivated.IsSuccess);
        Assert.Equal("Deactivated", deactivated.Value!.Status);
        Assert.True(reactivated.IsSuccess);
        Assert.Equal("Approved", reactivated.Value!.Status);
        Assert.True((await database.Context.Questions.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task ToggleQuestionActive_ByAnotherExpert_ReturnsForbiddenWithoutMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        var result = await new ToggleQuestionActiveCommandHandler(database.Context)
            .Handle(new ToggleQuestionActiveCommand(createResult.Value!.QuestionId, false, "expert-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionMutationForbidden, result.Error);
        Assert.True((await database.Context.Questions.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task CreateTagTopic_WithSameGradeParent_CreatesActiveChild()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "parent", 11, isRoot: true);

        var result = await new CreateTagTopicCommandHandler(database.Context)
            .Handle(
                new CreateTagTopicCommand(new CreateTagTopicRequest
                {
                    ParentTagId = "parent",
                    TagName = "Child topic",
                    Description = "  Child description  ",
                    Grade = 11,
                    DisplayOrder = 2
                }),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        var child = Assert.Single(await database.Context.TagTopics.Where(item => item.TagName == "Child topic").ToListAsync());
        Assert.Equal("parent", child.ParentTagId);
        Assert.True(child.IsActive);
        Assert.Equal("Child description", child.Description);
    }

    [Fact]
    public async Task CreateTagTopic_WithDifferentGradeParent_ReturnsParentInvalid()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "parent", 10, isRoot: true);

        var result = await new CreateTagTopicCommandHandler(database.Context)
            .Handle(
                new CreateTagTopicCommand(new CreateTagTopicRequest
                {
                    ParentTagId = "parent",
                    TagName = "Wrong-grade child",
                    Grade = 11,
                    DisplayOrder = 2
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagParentInvalid, result.Error);
        Assert.Single(await database.Context.TagTopics.ToListAsync());
    }

    [Fact]
    public async Task CreateTagTopic_WithInactiveParent_ReturnsParentInvalidWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "inactive-parent", 10, isActive: false, isRoot: true);

        var result = await new CreateTagTopicCommandHandler(database.Context)
            .Handle(
                new CreateTagTopicCommand(new CreateTagTopicRequest
                {
                    ParentTagId = "inactive-parent",
                    TagName = "Child of inactive parent",
                    Grade = 10,
                    DisplayOrder = 1
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagParentInvalid, result.Error);
        Assert.Single(await database.Context.TagTopics.ToListAsync());
    }

    [Fact]
    public async Task CreateTagTopic_WithMissingParent_ReturnsParentNotFoundWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();

        var result = await new CreateTagTopicCommandHandler(database.Context)
            .Handle(
                new CreateTagTopicCommand(new CreateTagTopicRequest
                {
                    ParentTagId = "missing-parent",
                    TagName = "Child with missing parent",
                    Grade = 10,
                    DisplayOrder = 1
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagParentNotFound, result.Error);
        Assert.Empty(await database.Context.TagTopics.ToListAsync());
    }

    [Fact]
    public async Task UpdateTagTopic_WhenReactivatingChildUnderInactiveParent_ReturnsParentInvalidWithoutMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "inactive-parent", 10, isActive: false, isRoot: true);
        await AddTopicAsync(database, "inactive-child", 10, isActive: false, parentTagId: "inactive-parent");

        var result = await new UpdateTagTopicCommandHandler(database.Context)
            .Handle(
                new UpdateTagTopicCommand("inactive-child", new UpdateTagTopicRequest
                {
                    ParentTagId = "inactive-parent",
                    TagName = "inactive-child",
                    Grade = 10,
                    DisplayOrder = 1,
                    IsActive = true
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagParentInvalid, result.Error);
        var child = await database.Context.TagTopics.SingleAsync(topic => topic.TagId == "inactive-child");
        Assert.False(child.IsActive);
    }

    [Fact]
    public async Task CreateTagTopic_WithInactiveAncestor_ReturnsParentInvalidWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddTopicAsync(database, "inactive-grandparent", 10, isActive: false, isRoot: true);
        await AddTopicAsync(database, "active-parent", 10, parentTagId: "inactive-grandparent");

        var result = await new CreateTagTopicCommandHandler(database.Context)
            .Handle(
                new CreateTagTopicCommand(new CreateTagTopicRequest
                {
                    ParentTagId = "active-parent",
                    TagName = "Child under hidden lineage",
                    Grade = 10,
                    DisplayOrder = 1
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagParentInvalid, result.Error);
        Assert.Equal(2, await database.Context.TagTopics.CountAsync());
    }

    [Fact]
    public async Task CreateTagDifficulty_WithDuplicateLevelValue_ReturnsConflictWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);

        var result = await new CreateTagDifficultyCommandHandler(database.Context)
            .Handle(
                new CreateTagDifficultyCommand(new CreateTagDifficultyRequest
                {
                    DifficultyName = "Also easy",
                    LevelValue = 1,
                    DisplayOrder = 2
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagLevelValueDuplicate, result.Error);
        Assert.Single(await database.Context.TagDifficulties.ToListAsync());
    }

    [Fact]
    public async Task CreateTagDifficulty_WithValidRequest_CreatesActiveDifficulty()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();

        var result = await new CreateTagDifficultyCommandHandler(database.Context)
            .Handle(
                new CreateTagDifficultyCommand(new CreateTagDifficultyRequest
                {
                    DifficultyName = "  Advanced  ",
                    Description = "  Higher-order reasoning  ",
                    LevelValue = 4,
                    DisplayOrder = 4
                }),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        var difficulty = Assert.Single(await database.Context.TagDifficulties.ToListAsync());
        Assert.Equal("Advanced", difficulty.DifficultyName);
        Assert.Equal("Higher-order reasoning", difficulty.Description);
        Assert.True(difficulty.IsActive);
    }

    [Fact]
    public async Task CreateTagDifficulty_WithNonPositiveLevel_ReturnsValidationErrorWithoutWrite()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();

        var result = await new CreateTagDifficultyCommandHandler(database.Context)
            .Handle(
                new CreateTagDifficultyCommand(new CreateTagDifficultyRequest
                {
                    DifficultyName = "Invalid level",
                    LevelValue = 0,
                    DisplayOrder = 1
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagLevelValueInvalid, result.Error);
        Assert.Empty(await database.Context.TagDifficulties.ToListAsync());
    }

    [Fact]
    public async Task UpdateTagDifficulty_WithSameLevel_RenamesAndDeactivates()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 2);

        var result = await new UpdateTagDifficultyCommandHandler(database.Context)
            .Handle(
                new UpdateTagDifficultyCommand("difficulty-1", new UpdateTagDifficultyRequest
                {
                    DifficultyName = "Medium renamed",
                    Description = "Updated",
                    LevelValue = 2,
                    DisplayOrder = 4,
                    IsActive = false
                }),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        var difficulty = await database.Context.TagDifficulties.SingleAsync();
        Assert.Equal("Medium renamed", difficulty.DifficultyName);
        Assert.False(difficulty.IsActive);
        Assert.Equal(4, difficulty.DisplayOrder);
    }

    [Fact]
    public async Task UpdateTagDifficulty_WhenChangingSystemLevel_ReturnsImmutableErrorWithoutMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 2);

        var result = await new UpdateTagDifficultyCommandHandler(database.Context)
            .Handle(
                new UpdateTagDifficultyCommand("difficulty-1", new UpdateTagDifficultyRequest
                {
                    DifficultyName = "Renamed",
                    LevelValue = 3,
                    DisplayOrder = 2,
                    IsActive = true
                }),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagLevelValueImmutable, result.Error);
        var difficulty = await database.Context.TagDifficulties.SingleAsync();
        Assert.Equal("difficulty-1", difficulty.DifficultyName);
        Assert.Equal(2, difficulty.LevelValue);
    }

    [Fact]
    public async Task DeleteTagDifficulty_ExistingDifficulty_SoftDeletesIt()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);

        var result = await new DeleteTagDifficultyCommandHandler(database.Context)
            .Handle(new DeleteTagDifficultyCommand("difficulty-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var difficulty = await database.Context.TagDifficulties.SingleAsync();
        Assert.False(difficulty.IsActive);
        Assert.Equal("SoftDeleted", result.Value!.DeleteMode);
    }

    [Fact]
    public async Task DeleteTagDifficulty_WhenMissing_ReturnsNotFound()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();

        var result = await new DeleteTagDifficultyCommandHandler(database.Context)
            .Handle(new DeleteTagDifficultyCommand("missing-difficulty"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.TagDifficultyNotFound, result.Error);
    }

    [Fact]
    public async Task DeleteTagDifficulty_WhenReferencedByQuestion_PreservesQuestionHistoryAndHidesTagFromActiveSelector()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var questionResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(questionResult.IsSuccess);

        var result = await new DeleteTagDifficultyCommandHandler(database.Context)
            .Handle(new DeleteTagDifficultyCommand("difficulty-1"), CancellationToken.None);
        var activeDifficulties = await new GetTagDifficultiesQueryHandler(database.Context)
            .Handle(new GetTagDifficultiesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(await database.Context.Questions.ToListAsync());
        Assert.Equal("difficulty-1", question.DifficultyId);
        Assert.Empty(activeDifficulties);
    }

    [Fact]
    public async Task DeleteLeafTopic_WhenReferencedByQuestion_PreservesQuestionHistoryAndHidesTagFromActiveSelector()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var questionResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        Assert.True(questionResult.IsSuccess);

        var result = await new DeleteTagTopicCommandHandler(database.Context)
            .Handle(new DeleteTagTopicCommand("topic-1"), CancellationToken.None);
        var activeTopics = await new GetTagTopicTreeQueryHandler(database.Context)
            .Handle(new GetTagTopicTreeQuery(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var questionTopic = Assert.Single(await database.Context.QuestionTopics.ToListAsync());
        Assert.Equal("topic-1", questionTopic.TagId);
        var root = Assert.Single(activeTopics);
        Assert.Equal("root-topic-1", root.TagId);
        Assert.Empty(root.Children);
    }

    [Fact]
    public async Task RetryScoreAdjustment_WhenEligibleAndOwned_InvokesServiceAndReturnsAdjustedTime()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = new Question
        {
            QuestionId = "question-1",
            QuestionContent = "Question",
            SolutionContent = "Solution",
            DifficultyId = "difficulty-1",
            Grade = 10,
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true
        };
        var report = new QuestionReport
        {
            ReportId = "report-1",
            QuestionId = question.QuestionId,
            ReporterAccountId = "student-1",
            ReporterRole = "Student",
            ReportReason = "Wrong answer",
            Status = "Resolved",
            ResolutionAction = "InvalidateAndAwardFull",
            CreatedTime = DateTime.UtcNow
        };
        database.Context.AddRange(question, report);
        await database.Context.SaveChangesAsync();
        var service = new RecordingScoreAdjustmentService(database.Context);

        var result = await new RetryScoreAdjustmentCommandHandler(database.Context, service)
            .Handle(new RetryScoreAdjustmentCommand(report.ReportId, "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(report.ReportId, service.AdjustedReportId);
        Assert.NotNull(result.Value!.ScoreAdjustedTime);
    }

    [Fact]
    public async Task RetryScoreAdjustment_WhenReportBelongsToAnotherExpert_ReturnsForbiddenWithoutCallingService()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = new Question
        {
            QuestionId = "question-1",
            QuestionContent = "Question",
            SolutionContent = "Solution",
            DifficultyId = "difficulty-1",
            Grade = 10,
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true
        };
        var report = new QuestionReport
        {
            ReportId = "report-1",
            QuestionId = question.QuestionId,
            ReporterAccountId = "student-1",
            ReporterRole = "Student",
            ReportReason = "Wrong answer",
            Status = "Resolved",
            ResolutionAction = "InvalidateAndAwardFull",
            CreatedTime = DateTime.UtcNow
        };
        database.Context.AddRange(question, report);
        await database.Context.SaveChangesAsync();
        var service = new RecordingScoreAdjustmentService(database.Context);

        var result = await new RetryScoreAdjustmentCommandHandler(database.Context, service)
            .Handle(new RetryScoreAdjustmentCommand(report.ReportId, "expert-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, result.Error);
        Assert.Null(service.AdjustedReportId);
    }

    [Fact]
    public async Task RetryScoreAdjustment_WhenAlreadyAdjusted_ReturnsNotRetryableWithoutCallingService()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = new Question
        {
            QuestionId = "question-1",
            QuestionContent = "Question",
            SolutionContent = "Solution",
            DifficultyId = "difficulty-1",
            Grade = 10,
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true
        };
        var report = new QuestionReport
        {
            ReportId = "report-1",
            QuestionId = question.QuestionId,
            ReporterAccountId = "student-1",
            ReporterRole = "Student",
            ReportReason = "Wrong answer",
            Status = "Resolved",
            ResolutionAction = "InvalidateAndAwardFull",
            ScoreAdjustedTime = DateTime.UtcNow,
            CreatedTime = DateTime.UtcNow
        };
        database.Context.AddRange(question, report);
        await database.Context.SaveChangesAsync();
        var service = new RecordingScoreAdjustmentService(database.Context);

        var result = await new RetryScoreAdjustmentCommandHandler(database.Context, service)
            .Handle(new RetryScoreAdjustmentCommand(report.ReportId, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ScoreAdjustmentNotRetryable, result.Error);
        Assert.Null(service.AdjustedReportId);
    }

    [Fact]
    public async Task CreateQuestion_WithTextualShortAnswer_RejectsNumericAnswerKey()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "SHORT_ANSWER";
        request.Answers = [new CreateAnswerRequest { AnswerContent = "π", IsCorrect = true }];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionShortAnswerNumericRequired, result.Error);
    }

    [Fact]
    public async Task UpdateQuestion_WithTextualShortAnswer_RejectsNumericAnswerKey()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var createResult = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(CreateQuestionRequest("difficulty-1", "topic-1"), "expert-1"), CancellationToken.None);
        var request = ToUpdateQuestionRequest(CreateQuestionRequest("difficulty-1", "topic-1"));
        request.QuestionType = "SHORT_ANSWER";
        request.Answers = [new CreateAnswerRequest { AnswerContent = "pi", IsCorrect = true }];

        var result = await new UpdateQuestionCommandHandler(database.Context)
            .Handle(new UpdateQuestionCommand(createResult.Value!.QuestionId, request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionShortAnswerNumericRequired, result.Error);
    }

    [Fact]
    public async Task CreateQuestion_WithNumericCompositeShortAnswerPart_AcceptsDecimalComma()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "COMPOSITE";
        request.Answers = [];
        request.Parts =
        [
            new CreateQuestionPartRequest
            {
                PartOrder = 1,
                PartLabel = "a",
                PartContent = "Tính kết quả.",
                PartType = "SHORT_ANSWER",
                CorrectText = "1,5",
                DefaultWeight = 1m
            }
        ];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateQuestion_WithTextualCompositeShortAnswerPart_RejectsNumericAnswerKey()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        await AddDifficultyAsync(database, "difficulty-1", 1);
        await AddTopicAsync(database, "topic-1", 10);
        var request = CreateQuestionRequest("difficulty-1", "topic-1");
        request.QuestionType = "COMPOSITE";
        request.Answers = [];
        request.Parts =
        [
            new CreateQuestionPartRequest
            {
                PartOrder = 1,
                PartContent = "Tính kết quả.",
                PartType = "SHORT_ANSWER",
                CorrectText = "vô nghiệm",
                DefaultWeight = 1m
            }
        ];

        var result = await new CreateQuestionCommandHandler(database.Context)
            .Handle(new CreateQuestionCommand(request, "expert-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionShortAnswerPartNumericRequired, result.Error);
    }

    private static CreateQuestionRequest CreateQuestionRequest(string difficultyId, string topicId) => new()
    {
        QuestionContent = "What is 2 + 2?",
        SolutionContent = "4",
        DifficultyId = difficultyId,
        Grade = 10,
        QuestionType = "SINGLE_CHOICE",
        DefaultWeight = 1m,
        Topics = [new CreateQuestionTopicRequest(topicId, true)],
        Answers =
        [
            new CreateAnswerRequest { AnswerContent = "4", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "5", IsCorrect = false }
        ]
    };

    private static UpdateQuestionRequest ToUpdateQuestionRequest(CreateQuestionRequest source) => new()
    {
        QuestionContent = source.QuestionContent,
        SolutionContent = source.SolutionContent,
        PictureUrl = source.PictureUrl,
        DifficultyId = source.DifficultyId,
        Grade = source.Grade,
        QuestionType = source.QuestionType,
        DefaultWeight = source.DefaultWeight,
        Topics = source.Topics,
        Answers = source.Answers,
        Parts = source.Parts
    };

    private static async Task AddTopicAsync(
        QuestionBankInMemoryContext database,
        string tagId,
        int grade,
        bool isActive = true,
        string? parentTagId = null,
        bool isRoot = false)
    {
        if (parentTagId is null && !isRoot)
        {
            var rootId = $"root-{tagId}";
            database.Context.TagTopics.Add(new TagTopic
            {
                TagId = rootId,
                TagName = rootId,
                Grade = grade,
                DisplayOrder = 1,
                IsActive = true
            });
            parentTagId = rootId;
        }

        database.Context.TagTopics.Add(new TagTopic
        {
            TagId = tagId,
            ParentTagId = parentTagId,
            TagName = tagId,
            Grade = grade,
            DisplayOrder = 1,
            IsActive = isActive
        });
        await database.Context.SaveChangesAsync();
    }

    private static async Task AddDifficultyAsync(QuestionBankInMemoryContext database, string difficultyId, int level, bool isActive = true)
    {
        database.Context.TagDifficulties.Add(new TagDifficulty
        {
            DifficultyId = difficultyId,
            DifficultyName = difficultyId,
            LevelValue = level,
            DisplayOrder = level,
            IsActive = isActive
        });
        await database.Context.SaveChangesAsync();
    }

    private sealed class RecordingScoreAdjustmentService(QuestionBank.Persistence.QuestionBankDbContext context)
        : IScoreAdjustmentService
    {
        public string? AdjustedReportId { get; private set; }

        public async Task AdjustInvalidQuestionVersionAsync(string reportId, CancellationToken cancellationToken = default)
        {
            AdjustedReportId = reportId;
            var report = await context.QuestionReports.SingleAsync(item => item.ReportId == reportId, cancellationToken);
            report.ScoreAdjustedTime = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

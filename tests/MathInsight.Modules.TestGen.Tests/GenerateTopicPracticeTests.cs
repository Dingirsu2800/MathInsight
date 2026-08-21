using MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Recommendations;
using MathInsight.Shared.Results;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class GenerateTopicPracticeTests
{
    [Fact]
    public async Task Generate_InsufficientPool_WritesNothing()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        var handler = CreateHandler(fixture);
        var result = await handler.Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Context.Tests);
    }

    [Fact]
    public async Task Generate_ValidPool_PersistsTenUnlimitedQuestionsWithNormalizedScore()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "topic", ParentTagId = "root", TagName = "Topic", Grade = 12, IsActive = true });
        fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"q-{index}");
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var test = Assert.Single(fixture.Context.Tests);
        Assert.Null(test.BlueprintId); Assert.Equal(0, test.DurationMinutes); Assert.Equal("NormalizedWeight", test.ScoringPolicy);
        Assert.Equal(10, test.Questions.Count); Assert.Equal(10m, test.Questions.Sum(question => question.MaxPointsSnapshot));
        Assert.All(test.Questions, question => Assert.Equal(TopicPracticePolicy.RuleVersion, question.RuleVersion));
        Assert.Equal(0, result.Value!.CreatedTime.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public async Task Generate_QualifiedMastery_PersistsMasteryAudit()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "topic", ParentTagId = "root", TagName = "Topic", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "weak-child", ParentTagId = "root", TagName = "Weak child", Grade = 12, IsActive = true, DisplayOrder = 1 });
        fixture.Context.TagDifficulties.AddRange(
            new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = "d-2", DifficultyName = "Understand", LevelValue = 2, IsActive = true });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"focus-{index}", "topic");
        AddQuestion(fixture, "focus-level-2", "topic", 12, "d-2");
        await fixture.Context.SaveChangesAsync();

        var advice = new TopicMasteryAdvice("topic", 1.40m, 5, 2, 1);
        var result = await CreateHandler(
            fixture,
            new AdviceRecommendationResolver("topic", new TopicPracticeRecommendationContext(
                true,
                advice,
                new HashSet<string>(["topic"], StringComparer.OrdinalIgnoreCase))))
            .Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasAdaptive);
        Assert.Equal("topic", result.Value.WeakTagId);
        Assert.Equal(10, result.Value.AdaptiveQuestionCount);
        Assert.Equal(0, result.Value.FallbackQuestionCount);
        var test = Assert.Single(fixture.Context.Tests);
        var focusRows = test.Questions.Where(question => question.IsAdaptiveSelected).ToList();
        Assert.Equal(10, focusRows.Count);
        Assert.All(focusRows, question =>
        {
            Assert.Equal("TopicPractice", question.SelectionReason);
            Assert.Equal("topic", question.RecommendedForTagId);
            Assert.Equal("d-1", question.RecommendedDifficultyId);
            Assert.Equal(1.40m, question.PtagAtSelection);
            Assert.Equal(TopicPracticePolicy.MasteryRuleVersion, question.RuleVersion);
        });
        Assert.All(test.Questions.Where(question => !question.IsAdaptiveSelected), question =>
        {
            Assert.Equal("TopicPractice", question.SelectionReason);
            Assert.Equal("topic", question.RecommendedForTagId);
            Assert.Null(question.RecommendedDifficultyId);
            Assert.Null(question.PtagAtSelection);
            Assert.Equal(TopicPracticePolicy.MasteryRuleVersion, question.RuleVersion);
        });
    }

    [Fact]
    public async Task Generate_ManualDifficulty_UsesExactlySelectedDifficultyWithoutCallingRecommender()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "topic", ParentTagId = "root", TagName = "Topic", Grade = 12, IsActive = true });
        fixture.Context.TagDifficulties.AddRange(
            new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = "d-3", DifficultyName = "Hard", LevelValue = 3, IsActive = true });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"hard-{index}", "topic", 12, "d-3");
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"easy-{index}", "topic", 12, "d-1");
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture, new ThrowingRecommendationResolver())
            .Handle(new GenerateTopicPracticeCommand("student", "topic", "d-3"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Manual", result.Value!.DifficultySelectionMode);
        Assert.Equal("d-3", result.Value.SelectedDifficultyId);
        Assert.All(Assert.Single(fixture.Context.Tests).Questions, question =>
        {
            Assert.StartsWith("hard-", question.QuestionId);
            Assert.Equal("d-3", question.RecommendedDifficultyId);
            Assert.False(question.IsAdaptiveSelected);
            Assert.Equal("TopicPractice-Manual-v1", question.RuleVersion);
        });
    }

    [Fact]
    public async Task Generate_ManualDifficultyWithNineCandidates_ReturnsConflictWithoutWrites()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "topic", ParentTagId = "root", TagName = "Topic", Grade = 12, IsActive = true });
        fixture.Context.TagDifficulties.AddRange(
            new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = "d-3", DifficultyName = "Hard", LevelValue = 3, IsActive = true });
        for (var index = 0; index < 9; index++) AddQuestion(fixture, $"hard-{index}", "topic", 12, "d-3");
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"easy-{index}", "topic", 12, "d-1");
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture, new ThrowingRecommendationResolver())
            .Handle(new GenerateTopicPracticeCommand("student", "topic", "d-3"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.TopicPracticeInsufficientQuestions.Code, result.Error!.Code);
        Assert.Empty(fixture.Context.Tests);
    }

    [Fact]
    public async Task Generate_RecommendationFailure_WritesNothing()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "topic", ParentTagId = "root", TagName = "Topic", Grade = 12, IsActive = true });
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(
            fixture,
            new FailingRecommendationResolver(TestGenerationErrors.TopicPracticeRecommenderUnavailable))
            .Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.TopicPracticeRecommenderUnavailable.Code, result.Error!.Code);
        Assert.Empty(fixture.Context.Tests);
    }

    [Fact]
    public async Task Generate_GradeTwelveStudent_CanGenerateSelectedGradeTenDirectChildOnly()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root-10", TagName = "Root 10", Grade = 10, IsActive = true },
            new TagTopicReadModel { TagId = "child-10", ParentTagId = "root-10", TagName = "Child 10", Grade = 10, IsActive = true },
            new TagTopicReadModel { TagId = "other-10", ParentTagId = "root-10", TagName = "Other 10", Grade = 10, IsActive = true });
        fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"child-{index}", "child-10", 10);
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"other-{index}", "other-10", 10);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(new GenerateTopicPracticeCommand("student", "child-10"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("child-10", result.Value!.SelectedTagId);
        Assert.All(Assert.Single(fixture.Context.Tests).Questions, question => Assert.StartsWith("child-", question.QuestionId));
    }

    [Fact]
    public async Task Generate_GradeElevenStudent_IsBlockedFromGradeTwelveTopic()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 11 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root-12", TagName = "Root 12", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "child-12", ParentTagId = "root-12", TagName = "Child 12", Grade = 12, IsActive = true });
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(new GenerateTopicPracticeCommand("student", "child-12"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_GRADE_NOT_ALLOWED", result.Error!.Code);
    }

    [Fact]
    public async Task Generate_RootTopic_IsNotAssignable()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.Add(new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = true });
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(new GenerateTopicPracticeCommand("student", "root"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PARENT_NOT_ASSIGNABLE", result.Error!.Code);
    }

    [Fact]
    public async Task Generate_TopicWithInactiveParent_IsNotAssignable()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "root", TagName = "Root", Grade = 12, IsActive = false },
            new TagTopicReadModel { TagId = "child", ParentTagId = "root", TagName = "Child", Grade = 12, IsActive = true });
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(new GenerateTopicPracticeCommand("student", "child"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PARENT_NOT_ASSIGNABLE", result.Error!.Code);
    }

    private static void AddQuestion(TestGenInMemoryContext fixture, string id, string tagId = "topic", int grade = 12, string difficultyId = "d-1")
    {
        fixture.Context.Questions.Add(new QuestionReadModel { QuestionId = id, Grade = grade, DifficultyId = difficultyId, QuestionType = "SingleChoice", Status = "Approved", IsActive = true, DefaultWeight = 1m });
        fixture.Context.QuestionTopics.Add(new QuestionTopicReadModel { QuestionTopicId = $"{id}-topic", QuestionId = id, TagId = tagId, IsPrimary = true });
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel { VersionId = $"{id}-v", QuestionId = id, VersionNumber = 1, SnapshotSchemaVersion = 2, AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(id, "SingleChoice", difficultyId, grade, 1m, [new QuestionTopicSnapshot(tagId, true)], [new QuestionAnswerSnapshot($"{id}-answer", "A", true)], [], "content", "solution")), CreatedTime = DateTime.UtcNow });
        fixture.Context.Answers.Add(new AnswerReadModel { AnswerId = $"{id}-answer", QuestionId = id, IsCorrect = true });
    }

    private static GenerateTopicPracticeCommandHandler CreateHandler(
        TestGenInMemoryContext fixture,
        ITopicPracticeRecommendationResolver? resolver = null) => new(
        fixture.Context,
        new QuestionCandidateCatalog(fixture.Context),
        new TopicPracticeQuestionSelector(new NoOpRandomizer()),
        resolver ?? new BaselineRecommendationResolver(),
        NullLogger<GenerateTopicPracticeCommandHandler>.Instance);

    private sealed class BaselineRecommendationResolver : ITopicPracticeRecommendationResolver
    {
        public Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
            string studentId,
            IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, TopicPracticeRecommendationContext> contexts = activeGradeTopics.ToDictionary(
                topic => topic.TagId,
                _ => TopicPracticeRecommendationContext.Baseline,
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(contexts));
        }
    }

    private sealed class AdviceRecommendationResolver(
        string selectedTagId,
        TopicPracticeRecommendationContext context) : ITopicPracticeRecommendationResolver
    {
        public Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
            string studentId,
            IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, TopicPracticeRecommendationContext> contexts = activeGradeTopics.ToDictionary(
                topic => topic.TagId,
                topic => string.Equals(topic.TagId, selectedTagId, StringComparison.OrdinalIgnoreCase)
                    ? context
                    : TopicPracticeRecommendationContext.Baseline,
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(contexts));
        }
    }

    private sealed class FailingRecommendationResolver(Error error) : ITopicPracticeRecommendationResolver
    {
        public Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
            string studentId,
            IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(error));
    }

    private sealed class ThrowingRecommendationResolver : ITopicPracticeRecommendationResolver
    {
        public Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
            string studentId,
            IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Manual difficulty must not call the recommendation resolver.");
    }

    private sealed class NoOpRandomizer : IGenerationRandomizer { public void Shuffle<T>(IList<T> values) { } }
}

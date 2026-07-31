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
        fixture.Context.TagTopics.Add(new TagTopicReadModel { TagId = "topic", TagName = "Topic", Grade = 12, IsActive = true });
        fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"q-{index}");
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture);

        var result = await handler.Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var test = Assert.Single(fixture.Context.Tests);
        Assert.Null(test.BlueprintId); Assert.Equal(0, test.DurationMinutes); Assert.Equal("NormalizedWeight", test.ScoringPolicy);
        Assert.Equal(10, test.Questions.Count); Assert.Equal(10m, test.Questions.Sum(question => question.MaxPointsSnapshot));
        Assert.All(test.Questions, question => Assert.Equal("TopicPractice-v1", question.RuleVersion));
        Assert.Equal(0, result.Value!.CreatedTime.Ticks % TimeSpan.TicksPerSecond);
    }

    [Fact]
    public async Task Generate_QualifiedLevelOneAdvice_PersistsFocusAndGeneralAudit()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.AddRange(
            new TagTopicReadModel { TagId = "topic", TagName = "Parent", Grade = 12, IsActive = true },
            new TagTopicReadModel { TagId = "weak-child", ParentTagId = "topic", TagName = "Weak child", Grade = 12, IsActive = true, DisplayOrder = 1 });
        fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", LevelValue = 1, IsActive = true });
        for (var index = 0; index < 8; index++) AddQuestion(fixture, $"focus-{index}", "weak-child");
        for (var index = 0; index < 2; index++) AddQuestion(fixture, $"general-{index}", "topic");
        await fixture.Context.SaveChangesAsync();

        var advice = new WeakTagAdvice("weak-child", "Weak child", 2.40m, 5, 1, "BottleneckSubTag");
        var result = await CreateHandler(
            fixture,
            new AdviceRecommendationResolver("topic", new TopicPracticeRecommendationContext(
                true,
                advice,
                new HashSet<string>(["weak-child"], StringComparer.OrdinalIgnoreCase))))
            .Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasAdaptive);
        Assert.Equal("weak-child", result.Value.WeakTagId);
        Assert.Equal(8, result.Value.AdaptiveQuestionCount);
        Assert.Equal(2, result.Value.FallbackQuestionCount);
        var test = Assert.Single(fixture.Context.Tests);
        var focusRows = test.Questions.Where(question => question.IsAdaptiveSelected).ToList();
        Assert.Equal(8, focusRows.Count);
        Assert.All(focusRows, question =>
        {
            Assert.Equal("WeakTagPractice", question.SelectionReason);
            Assert.Equal("weak-child", question.RecommendedForTagId);
            Assert.Equal("d-1", question.RecommendedDifficultyId);
            Assert.Equal(2.40m, question.PtagAtSelection);
            Assert.Equal("TopicPractice-WeakTag-v1", question.RuleVersion);
        });
        Assert.All(test.Questions.Where(question => !question.IsAdaptiveSelected), question =>
        {
            Assert.Equal("TopicPractice", question.SelectionReason);
            Assert.Equal("topic", question.RecommendedForTagId);
            Assert.Null(question.RecommendedDifficultyId);
            Assert.Null(question.PtagAtSelection);
            Assert.Equal("TopicPractice-WeakTag-v1", question.RuleVersion);
        });
    }

    [Fact]
    public async Task Generate_RecommendationFailure_WritesNothing()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
        fixture.Context.TagTopics.Add(new TagTopicReadModel { TagId = "topic", TagName = "Topic", Grade = 12, IsActive = true });
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(
            fixture,
            new FailingRecommendationResolver(TestGenerationErrors.TopicPracticeRecommenderUnavailable))
            .Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.TopicPracticeRecommenderUnavailable.Code, result.Error!.Code);
        Assert.Empty(fixture.Context.Tests);
    }

    private static void AddQuestion(TestGenInMemoryContext fixture, string id, string tagId = "topic")
    {
        fixture.Context.Questions.Add(new QuestionReadModel { QuestionId = id, Grade = 12, DifficultyId = "d-1", QuestionType = "SingleChoice", Status = "Approved", IsActive = true, DefaultWeight = 1m });
        fixture.Context.QuestionTopics.Add(new QuestionTopicReadModel { QuestionTopicId = $"{id}-topic", QuestionId = id, TagId = tagId, IsPrimary = true });
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel { VersionId = $"{id}-v", QuestionId = id, VersionNumber = 1, SnapshotSchemaVersion = 2, AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(id, "SingleChoice", "d-1", 12, 1m, [new QuestionTopicSnapshot(tagId, true)], [new QuestionAnswerSnapshot($"{id}-answer", "A", true)], [], "content", "solution")), CreatedTime = DateTime.UtcNow });
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

    private sealed class NoOpRandomizer : IGenerationRandomizer { public void Shuffle<T>(IList<T> values) { } }
}

using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Recommendations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class AdaptiveBlueprintExamGenerationTests
{
    private const string StudentId = "student-adaptive";
    private const string BlueprintId = "blueprint-adaptive";
    private const string WeakTopic = "topic-weak";
    private const string NeutralTopic = "topic-neutral";
    private const string StrongTopic = "topic-strong";
    private const string DifficultyOne = "difficulty-1";
    private const string DifficultyTwo = "difficulty-2";
    private const string DifficultyThree = "difficulty-3";
    private const string DifficultyFour = "difficulty-4";

    [Fact]
    public async Task HandlerCannotResolveWithoutMasteryProvider()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var services = new ServiceCollection();
        services.AddSingleton(testContext.Context);
        services.AddSingleton<IBlueprintExamCandidateProvider>(
            new CapturingCandidateProvider([]));
        services.AddSingleton<IAdaptiveBlueprintExamQuestionSelector>(
            new AdaptiveBlueprintExamQuestionSelector(new NoOpGenerationRandomizer()));
        services.AddTransient<GenerateBlueprintExamCommandHandler>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => serviceProvider.GetRequiredService<GenerateBlueprintExamCommandHandler>());
    }

    [Fact]
    public async Task Generate_CallsMasteryOnceAndPersistsOnlyActuallyAdjustedRows()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext.Context);
        AddDifficulties(testContext.Context);
        var blueprint = AddBlueprint(testContext.Context);
        var provider = new CapturingCandidateProvider([
            Candidate("weak-preferred", WeakTopic, DifficultyOne),
            Candidate("neutral-original", NeutralTopic, DifficultyTwo),
            Candidate("strong-preferred", StrongTopic, DifficultyFour)
        ]);
        var mastery = new CapturingMasteryProvider(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            [WeakTopic] = new(WeakTopic, 4.99m, 3, 2),
            [NeutralTopic] = new(NeutralTopic, 5m, 3, 2),
            [StrongTopic] = new(StrongTopic, 7.5m, 3, 4)
        });
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext, provider, mastery).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(1, mastery.CallCount);
        Assert.Equal(
            [NeutralTopic, StrongTopic, WeakTopic],
            mastery.RequestedTagIds);
        Assert.Equal([DifficultyOne, DifficultyTwo, DifficultyThree, DifficultyFour], provider.RequestedDifficultyIds);
        Assert.True(response.WasAdaptive);
        Assert.Equal(2, response.AdaptiveQuestionCount);
        Assert.Equal(1, response.BaselineQuestionCount);
        Assert.Equal(AdaptiveBlueprintExamPolicy.RuleVersion, response.RuleVersion);

        var questions = await testContext.Context.TestQuestions
            .OrderBy(question => question.QuestionOrder)
            .ToListAsync();
        Assert.Contains(questions, question =>
            question.QuestionId == "weak-preferred" &&
            question.IsAdaptiveSelected &&
            question.RecommendedForTagId == WeakTopic &&
            question.RecommendedDifficultyId == DifficultyOne &&
            question.PtagAtSelection == 4.99m &&
            question.RuleVersion == AdaptiveBlueprintExamPolicy.RuleVersion);
        Assert.Contains(questions, question =>
            question.QuestionId == "strong-preferred" &&
            question.IsAdaptiveSelected &&
            question.RecommendedForTagId == StrongTopic &&
            question.RecommendedDifficultyId == DifficultyFour);
        Assert.Contains(questions, question =>
            question.QuestionId == "neutral-original" &&
            !question.IsAdaptiveSelected &&
            question.RecommendedForTagId is null &&
            question.RecommendedDifficultyId is null &&
            question.PtagAtSelection is null &&
            question.RuleVersion is null);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);
    }

    [Fact]
    public async Task Generate_MissingOrInsufficientEvidenceKeepsOriginalDifficulty()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext.Context);
        AddDifficulties(testContext.Context);
        AddBlueprint(testContext.Context, [
            (WeakTopic, DifficultyTwo),
            (NeutralTopic, DifficultyThree)
        ]);
        var provider = new CapturingCandidateProvider([
            Candidate("weak-original", WeakTopic, DifficultyTwo),
            Candidate("neutral-original", NeutralTopic, DifficultyThree)
        ]);
        var mastery = new CapturingMasteryProvider(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            [WeakTopic] = new(WeakTopic, 1m, 2, 1)
        });
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext, provider, mastery).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.False(response.WasAdaptive);
        Assert.Equal(0, response.AdaptiveQuestionCount);
        Assert.Equal(2, response.BaselineQuestionCount);
        Assert.All(await testContext.Context.TestQuestions.ToListAsync(), question =>
        {
            Assert.False(question.IsAdaptiveSelected);
            Assert.Null(question.RecommendedDifficultyId);
            Assert.Null(question.PtagAtSelection);
        });
    }

    [Fact]
    public async Task Generate_UsesOriginalDifficultyWhenPreferredPoolIsShort()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext.Context);
        AddDifficulties(testContext.Context);
        AddBlueprint(testContext.Context, [(WeakTopic, DifficultyTwo)]);
        var provider = new CapturingCandidateProvider([Candidate("weak-original", WeakTopic, DifficultyTwo)]);
        var mastery = new CapturingMasteryProvider(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            [WeakTopic] = new(WeakTopic, 1m, 5, 1)
        });
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext, provider, mastery).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(await testContext.Context.TestQuestions.ToListAsync());
        Assert.False(question.IsAdaptiveSelected);
        Assert.Null(question.RecommendedForTagId);
        Assert.Null(question.RecommendedDifficultyId);
        Assert.Null(question.PtagAtSelection);
        Assert.Null(question.RuleVersion);
    }

    [Fact]
    public async Task Generate_ReturnsUnavailableAndWritesNothingWhenProviderThrows()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext.Context);
        AddDifficulties(testContext.Context);
        AddBlueprint(testContext.Context);
        var provider = new CapturingCandidateProvider([Candidate("question", WeakTopic, DifficultyTwo)]);
        var mastery = new CapturingMasteryProvider(exception: new InvalidOperationException("provider down"));
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext, provider, mastery).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.AdaptiveExamMasteryUnavailable, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    [Fact]
    public async Task Generate_ReturnsInvalidAndWritesNothingWhenProviderAdviceIsMalformed()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddStudent(testContext.Context);
        AddDifficulties(testContext.Context);
        AddBlueprint(testContext.Context);
        var provider = new CapturingCandidateProvider([Candidate("question", WeakTopic, DifficultyTwo)]);
        var mastery = new CapturingMasteryProvider(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            [WeakTopic] = new(WeakTopic, 10.01m, 3, 1)
        });
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext, provider, mastery).Handle(
            new GenerateBlueprintExamCommand(BlueprintId, StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TestGenerationErrors.AdaptiveExamMasteryInvalid, result.Error);
        Assert.Empty(await testContext.Context.Tests.ToListAsync());
    }

    private static GenerateBlueprintExamCommandHandler CreateHandler(
        TestGenInMemoryContext testContext,
        CapturingCandidateProvider provider,
        CapturingMasteryProvider mastery)
        => new(
            testContext.Context,
            provider,
            new AdaptiveBlueprintExamQuestionSelector(new NoOpGenerationRandomizer()),
            mastery);

    private static Blueprint AddBlueprint(
        TestGenDbContext context,
        (string TagId, string DifficultyId)[]? slots = null)
    {
        slots ??= [
            (WeakTopic, DifficultyTwo),
            (NeutralTopic, DifficultyTwo),
            (StrongTopic, DifficultyThree)
        ];
        var blueprint = new Blueprint
        {
            BlueprintId = BlueprintId,
            BlueprintName = "Adaptive Blueprint",
            Grade = 12,
            TotalQuestions = slots.Length,
            TotalScore = 3m,
            DurationMinutes = 90,
            ExpertId = "expert",
            Status = BlueprintStatuses.Approved
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = $"{BlueprintId}-section",
            BlueprintId = BlueprintId,
            SectionOrder = 1,
            SectionName = "Section",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            TotalQuestions = slots.Length,
            ScoreBudget = 3m,
            ScoringRule = "AllOrNothing"
        };
        for (var index = 0; index < slots.Length; index++)
        {
            section.Details.Add(new BlueprintDetail
            {
                BlueprintDetailId = $"{BlueprintId}-detail-{index}",
                BlueprintId = BlueprintId,
                BlueprintSectionId = section.BlueprintSectionId,
                TagId = slots[index].TagId,
                DifficultyId = slots[index].DifficultyId,
                Quantity = 1
            });
        }

        blueprint.Sections.Add(section);
        context.Blueprints.Add(blueprint);
        return blueprint;
    }

    private static void AddStudent(TestGenDbContext context)
        => context.Students.Add(new StudentReadModel { StudentId = StudentId, CurrentGrade = 12 });

    private static void AddDifficulties(TestGenDbContext context)
    {
        context.TagDifficulties.AddRange(
            new TagDifficultyReadModel { DifficultyId = DifficultyOne, DifficultyName = "Level 1", LevelValue = 1, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = DifficultyTwo, DifficultyName = "Level 2", LevelValue = 2, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = DifficultyThree, DifficultyName = "Level 3", LevelValue = 3, IsActive = true },
            new TagDifficultyReadModel { DifficultyId = DifficultyFour, DifficultyName = "Level 4", LevelValue = 4, IsActive = true });
    }

    private static BlueprintExamCandidate Candidate(string id, string tagId, string difficultyId)
        => new(
            id,
            $"{id}-version",
            1m,
            difficultyId,
            BlueprintQuestionTypes.SingleChoice,
            new HashSet<string>([tagId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["AllOrNothing"], StringComparer.OrdinalIgnoreCase));

    private sealed class CapturingCandidateProvider : IBlueprintExamCandidateProvider
    {
        private readonly BlueprintExamCandidatePool _pool;

        public CapturingCandidateProvider(IReadOnlyList<BlueprintExamCandidate> candidates)
            => _pool = new BlueprintExamCandidatePool(candidates, []);

        public IReadOnlyList<string> RequestedDifficultyIds { get; private set; } = [];

        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(Blueprint blueprint, CancellationToken cancellationToken)
            => Task.FromResult(_pool);

        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(
            Blueprint blueprint,
            IReadOnlyCollection<string> difficultyIds,
            CancellationToken cancellationToken)
        {
            RequestedDifficultyIds = difficultyIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
            return Task.FromResult(_pool);
        }
    }

    private sealed class CapturingMasteryProvider : IStudentTopicMasteryProvider
    {
        private readonly IReadOnlyDictionary<string, TopicMasteryAdvice> _advice;
        private readonly Exception? _exception;

        public CapturingMasteryProvider(
            IReadOnlyDictionary<string, TopicMasteryAdvice>? advice = null,
            Exception? exception = null)
        {
            _advice = advice ?? new Dictionary<string, TopicMasteryAdvice>();
            _exception = exception;
        }

        public int CallCount { get; private set; }
        public IReadOnlyList<string> RequestedTagIds { get; private set; } = [];

        public Task<IReadOnlyDictionary<string, TopicMasteryAdvice>> GetTopicMasteryAdviceAsync(
            string studentId,
            IReadOnlyCollection<string> tagIds,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedTagIds = tagIds.ToList();
            if (_exception is not null)
                return Task.FromException<IReadOnlyDictionary<string, TopicMasteryAdvice>>(_exception);

            return Task.FromResult(_advice);
        }
    }

    private sealed class NoOpGenerationRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }
}

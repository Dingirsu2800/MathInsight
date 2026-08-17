using System.Text.Json;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Queries.GetTopicPracticeOptions;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Recommendations;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeOptionsTests
{
    [Fact]
    public async Task Options_ReturnsOnlyActiveDirectChildrenAtOrBelowStudentGrade()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "root-12", 12); AddTopic(fixture, "active", 12, "root-12"); AddTopic(fixture, "inactive", 12, "root-12", false); AddTopic(fixture, "root-11", 11); AddTopic(fixture, "grade-11", 11, "root-11");
        await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(["active", "grade-11"], result.Value!.Topics.Select(item => item.TagId));
    }

    [Fact]
    public async Task Options_GradeTwelveStudentCanSelectGradeTenDirectChild()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture);
        AddTopic(fixture, "root-10", 10);
        AddTopic(fixture, "child-10", 10, "root-10");
        await fixture.Context.SaveChangesAsync();

        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);

        var option = Assert.Single(result.Value!.Topics);
        Assert.Equal("child-10", option.TagId);
        Assert.Equal(10, option.Grade);
        Assert.Equal("root-10", option.ParentTagName);
    }

    [Fact]
    public async Task Options_ReturnsDirectChildCountWithoutMakingParentSelectable()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "parent", 12); AddTopic(fixture, "child", 12, "parent"); AddDifficulty(fixture); AddQuestion(fixture, "question", "child"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        var child = Assert.Single(result.Value!.Topics);
        Assert.Equal("child", child.TagId);
        Assert.Equal(1, child.AvailableQuestionCount);
    }

    [Fact]
    public async Task Options_CountCapsCompositeContributionAtTwo()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "root", 12); AddTopic(fixture, "topic", 12, "root"); AddDifficulty(fixture); for (var i = 0; i < 3; i++) AddQuestion(fixture, $"composite-{i}", "topic", "Composite"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.Equal(2, Assert.Single(result.Value!.Topics).AvailableQuestionCount);
    }

    [Fact]
    public async Task Options_MarksCanGenerateAtTenSelectableQuestions()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "root", 12); AddTopic(fixture, "topic", 12, "root"); AddDifficulty(fixture); for (var i = 0; i < 10; i++) AddQuestion(fixture, $"question-{i}", "topic"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(Assert.Single(result.Value!.Topics).CanGenerate);
    }

    [Fact]
    public async Task Options_ReturnsAvailabilityForEachActiveSupportedDifficulty()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture);
        AddTopic(fixture, "root", 12);
        AddTopic(fixture, "topic", 12, "root");
        AddDifficulty(fixture, "d-1", "Easy", 1);
        AddDifficulty(fixture, "d-3", "Hard", 3);
        fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-inactive", DifficultyName = "Inactive", IsActive = false, LevelValue = 2 });
        for (var index = 0; index < 10; index++) AddQuestion(fixture, $"easy-{index}", "topic", difficultyId: "d-1");
        for (var index = 0; index < 9; index++) AddQuestion(fixture, $"hard-{index}", "topic", difficultyId: "d-3");
        await fixture.Context.SaveChangesAsync();

        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);

        var availability = Assert.Single(result.Value!.Topics).DifficultyAvailability;
        Assert.Collection(
            availability,
            easy =>
            {
                Assert.Equal("d-1", easy.DifficultyId);
                Assert.Equal(10, easy.AvailableQuestionCount);
                Assert.True(easy.CanGenerate);
            },
            hard =>
            {
                Assert.Equal("d-3", hard.DifficultyId);
                Assert.Equal(9, hard.AvailableQuestionCount);
                Assert.False(hard.CanGenerate);
            });
    }

    [Fact]
    public async Task Options_MarksDirectChildWithOwnWeakRecommendation()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "parent", 12); AddTopic(fixture, "child", 12, "parent"); AddDifficulty(fixture);
        await fixture.Context.SaveChangesAsync();
        var resolver = new StubRecommendationResolver(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(
            new Dictionary<string, TopicPracticeRecommendationContext>(StringComparer.OrdinalIgnoreCase)
            {
                ["child"] = new(
                    true,
                    new TopicMasteryAdvice("child", 2.40m, 5, 1),
                    new HashSet<string>(["child"], StringComparer.OrdinalIgnoreCase))
            }));

        var result = await Handler(fixture, resolver).Handle(new("student"), CancellationToken.None);

        var child = Assert.Single(result.Value!.Topics, item => item.TagId == "child");
        Assert.True(child.IsWeakRecommended);
        Assert.Equal("child", child.WeakTagId);
        Assert.Equal("child", child.WeakTagName);
        Assert.Equal(2.40m, child.OfficialPoint);
        Assert.Equal(5, child.EvidenceCount);
        Assert.Equal((byte)1, child.RecommendedDifficultyLevel);
        Assert.Equal("TopicMastery", child.RecommendationReason);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    public async Task Options_RecommendationFailure_PropagatesStableError()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "root", 12); AddTopic(fixture, "topic", 12, "root"); await fixture.Context.SaveChangesAsync();
        var resolver = new StubRecommendationResolver(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(
            new Error("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", "Unavailable")));

        var result = await Handler(fixture, resolver).Handle(new("student"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", result.Error!.Code);
    }

    [Fact]
    public async Task Options_ReturnsStudentNotFoundWhenStudentDoesNotExist()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_STUDENT_NOT_FOUND", result.Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(9)]
    public async Task Options_ReturnsStudentGradeRequiredWhenCurrentGradeIsMissingOrInvalid(int? currentGrade)
    {
        await using var fixture = TestGenInMemoryContext.Create();
        fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = currentGrade });
        await fixture.Context.SaveChangesAsync();

        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("STUDENT_GRADE_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task Options_BatchesCandidateCatalogByGrade()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture);
        AddTopic(fixture, "root-10", 10);
        AddTopic(fixture, "child-10-a", 10, "root-10");
        AddTopic(fixture, "child-10-b", 10, "root-10");
        AddTopic(fixture, "root-11", 11);
        AddTopic(fixture, "child-11", 11, "root-11");
        await fixture.Context.SaveChangesAsync();
        var catalog = new RecordingCatalog();
        var handler = new GetTopicPracticeOptionsQueryHandler(fixture.Context, catalog, new StubRecommendationResolver());

        var result = await handler.Handle(new("student"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, catalog.Filters.Count);
        Assert.Contains(catalog.Filters, filter => filter.Grade == 10 && filter.TagIds.Count == 2);
        Assert.Contains(catalog.Filters, filter => filter.Grade == 11 && filter.TagIds.Count == 1);
    }

    private static GetTopicPracticeOptionsQueryHandler Handler(TestGenInMemoryContext fixture, ITopicPracticeRecommendationResolver? resolver = null)
        => new(fixture.Context, new QuestionCandidateCatalog(fixture.Context), resolver ?? new StubRecommendationResolver());
    private static void AddStudent(TestGenInMemoryContext fixture) => fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
    private static void AddTopic(TestGenInMemoryContext fixture, string id, int grade, string? parent = null, bool active = true) => fixture.Context.TagTopics.Add(new TagTopicReadModel { TagId = id, Grade = grade, ParentTagId = parent, TagName = id, IsActive = active });
    private static void AddDifficulty(TestGenInMemoryContext fixture, string id = "d-1", string name = "Easy", int level = 1) => fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = id, DifficultyName = name, IsActive = true, LevelValue = level });
    private static void AddQuestion(TestGenInMemoryContext fixture, string id, string tagId, string type = "SingleChoice", string difficultyId = "d-1")
    {
        fixture.Context.Questions.Add(new QuestionReadModel { QuestionId = id, Grade = 12, DifficultyId = difficultyId, QuestionType = type, Status = "Approved", IsActive = true, DefaultWeight = 1m });
        fixture.Context.QuestionTopics.Add(new QuestionTopicReadModel { QuestionTopicId = $"{id}-topic", QuestionId = id, TagId = tagId, IsPrimary = true });
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel { VersionId = $"{id}-v", QuestionId = id, VersionNumber = 1, SnapshotSchemaVersion = 2, AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(id, type, difficultyId, 12, 1m, [new QuestionTopicSnapshot(tagId, true)], type == "Composite" ? [] : [new QuestionAnswerSnapshot($"{id}-a", "A", true)], type == "Composite" ? [new QuestionPartSnapshot($"{id}-p", 1, "a", "part", "TrueFalse", true, null, null, null, null, 1m)] : [], "content", "solution")), CreatedTime = DateTime.UtcNow });
        if (type == "Composite") fixture.Context.QuestionParts.Add(new QuestionPartReadModel { PartId = $"{id}-p", QuestionId = id, PartOrder = 1, PartType = "TrueFalse", CorrectBoolean = true, DefaultWeight = 1m }); else fixture.Context.Answers.Add(new AnswerReadModel { AnswerId = $"{id}-a", QuestionId = id, IsCorrect = true });
    }

    private sealed class StubRecommendationResolver : ITopicPracticeRecommendationResolver
    {
        private readonly Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>? _result;

        public StubRecommendationResolver(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>? result = null)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
            string studentId,
            IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_result is not null)
                return Task.FromResult(_result);

            IReadOnlyDictionary<string, TopicPracticeRecommendationContext> baseline = activeGradeTopics.ToDictionary(
                topic => topic.TagId,
                _ => TopicPracticeRecommendationContext.Baseline,
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(baseline));
        }
    }

    private sealed class RecordingCatalog : IQuestionCandidateCatalog
    {
        public List<QuestionCandidateCatalogFilter> Filters { get; } = [];

        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(QuestionCandidateCatalogFilter filter, CancellationToken cancellationToken)
        {
            Filters.Add(filter);
            return Task.FromResult(new BlueprintExamCandidatePool([], []));
        }
    }
}

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
    public async Task Options_ReturnsOnlyActiveTopicsForStudentCurrentGrade()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "active", 12); AddTopic(fixture, "inactive", 12, active: false); AddTopic(fixture, "grade-11", 11);
        await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(["active"], result.Value!.Topics.Select(item => item.TagId));
    }

    [Fact]
    public async Task Options_ParentCountIncludesActiveDescendants()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "parent", 12); AddTopic(fixture, "child", 12, "parent"); AddDifficulty(fixture); AddQuestion(fixture, "question", "child"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.Equal(1, result.Value!.Topics.Single(item => item.TagId == "parent").AvailableQuestionCount);
    }

    [Fact]
    public async Task Options_CountCapsCompositeContributionAtTwo()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "topic", 12); AddDifficulty(fixture); for (var i = 0; i < 3; i++) AddQuestion(fixture, $"composite-{i}", "topic", "Composite"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.Equal(2, Assert.Single(result.Value!.Topics).AvailableQuestionCount);
    }

    [Fact]
    public async Task Options_MarksCanGenerateAtTenSelectableQuestions()
    {
        await using var fixture = TestGenInMemoryContext.Create(); AddStudent(fixture); AddTopic(fixture, "topic", 12); AddDifficulty(fixture); for (var i = 0; i < 10; i++) AddQuestion(fixture, $"question-{i}", "topic"); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(Assert.Single(result.Value!.Topics).CanGenerate);
    }

    [Fact]
    public async Task Options_MarksParentWithRepresentativeWeakDescendant()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "parent", 12); AddTopic(fixture, "child", 12, "parent"); AddDifficulty(fixture);
        await fixture.Context.SaveChangesAsync();
        var resolver = new StubRecommendationResolver(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(
            new Dictionary<string, TopicPracticeRecommendationContext>(StringComparer.OrdinalIgnoreCase)
            {
                ["parent"] = new(
                    true,
                    new WeakTagAdvice("child", "Child", 2.40m, 5, 1, "OfficialPointBelow5"),
                    new HashSet<string>(["child"], StringComparer.OrdinalIgnoreCase))
            }));

        var result = await Handler(fixture, resolver).Handle(new("student"), CancellationToken.None);

        var parent = Assert.Single(result.Value!.Topics, item => item.TagId == "parent");
        Assert.True(parent.IsWeakRecommended);
        Assert.Equal("child", parent.WeakTagId);
        Assert.Equal("Child", parent.WeakTagName);
        Assert.Equal(2.40m, parent.OfficialPoint);
        Assert.Equal(5, parent.EvidenceCount);
        Assert.Equal((byte)1, parent.RecommendedDifficultyLevel);
        Assert.Equal("OfficialPointBelow5", parent.RecommendationReason);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    public async Task Options_RecommendationFailure_PropagatesStableError()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddStudent(fixture); AddTopic(fixture, "topic", 12); await fixture.Context.SaveChangesAsync();
        var resolver = new StubRecommendationResolver(Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(
            new Error("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", "Unavailable")));

        var result = await Handler(fixture, resolver).Handle(new("student"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", result.Error!.Code);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    public async Task Options_ReturnsStudentNotFoundForMissingOrInvalidGrade(string mode)
    {
        await using var fixture = TestGenInMemoryContext.Create(); if (mode == "invalid") fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 9 }); await fixture.Context.SaveChangesAsync();
        var result = await Handler(fixture).Handle(new("student"), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_STUDENT_NOT_FOUND", result.Error!.Code);
    }

    private static GetTopicPracticeOptionsQueryHandler Handler(TestGenInMemoryContext fixture, ITopicPracticeRecommendationResolver? resolver = null)
        => new(fixture.Context, new QuestionCandidateCatalog(fixture.Context), resolver ?? new StubRecommendationResolver());
    private static void AddStudent(TestGenInMemoryContext fixture) => fixture.Context.Students.Add(new StudentReadModel { StudentId = "student", CurrentGrade = 12 });
    private static void AddTopic(TestGenInMemoryContext fixture, string id, int grade, string? parent = null, bool active = true) => fixture.Context.TagTopics.Add(new TagTopicReadModel { TagId = id, Grade = grade, ParentTagId = parent, TagName = id, IsActive = active });
    private static void AddDifficulty(TestGenInMemoryContext fixture) => fixture.Context.TagDifficulties.Add(new TagDifficultyReadModel { DifficultyId = "d-1", DifficultyName = "Easy", IsActive = true, LevelValue = 1 });
    private static void AddQuestion(TestGenInMemoryContext fixture, string id, string tagId, string type = "SingleChoice")
    {
        fixture.Context.Questions.Add(new QuestionReadModel { QuestionId = id, Grade = 12, DifficultyId = "d-1", QuestionType = type, Status = "Approved", IsActive = true, DefaultWeight = 1m });
        fixture.Context.QuestionTopics.Add(new QuestionTopicReadModel { QuestionTopicId = $"{id}-topic", QuestionId = id, TagId = tagId, IsPrimary = true });
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel { VersionId = $"{id}-v", QuestionId = id, VersionNumber = 1, SnapshotSchemaVersion = 2, AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(id, type, "d-1", 12, 1m, [new QuestionTopicSnapshot(tagId, true)], type == "Composite" ? [] : [new QuestionAnswerSnapshot($"{id}-a", "A", true)], type == "Composite" ? [new QuestionPartSnapshot($"{id}-p", 1, "a", "part", "TrueFalse", true, null, null, null, null, 1m)] : [], "content", "solution")), CreatedTime = DateTime.UtcNow });
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
}

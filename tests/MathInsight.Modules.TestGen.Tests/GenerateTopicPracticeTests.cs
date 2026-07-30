using MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Questions;
using System.Text.Json;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class GenerateTopicPracticeTests
{
    [Fact]
    public async Task Generate_InsufficientPool_WritesNothing()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        var handler = new GenerateTopicPracticeCommandHandler(fixture.Context, new QuestionCandidateCatalog(fixture.Context), new TopicPracticeQuestionSelector(new NoOpRandomizer()));
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
        var handler = new GenerateTopicPracticeCommandHandler(fixture.Context, new QuestionCandidateCatalog(fixture.Context), new TopicPracticeQuestionSelector(new NoOpRandomizer()));

        var result = await handler.Handle(new GenerateTopicPracticeCommand("student", "topic"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var test = Assert.Single(fixture.Context.Tests);
        Assert.Null(test.BlueprintId); Assert.Equal(0, test.DurationMinutes); Assert.Equal("NormalizedWeight", test.ScoringPolicy);
        Assert.Equal(10, test.Questions.Count); Assert.Equal(10m, test.Questions.Sum(question => question.MaxPointsSnapshot));
        Assert.All(test.Questions, question => Assert.Equal("TopicPractice-v1", question.RuleVersion));
    }

    private static void AddQuestion(TestGenInMemoryContext fixture, string id)
    {
        fixture.Context.Questions.Add(new QuestionReadModel { QuestionId = id, Grade = 12, DifficultyId = "d-1", QuestionType = "SingleChoice", Status = "Approved", IsActive = true, DefaultWeight = 1m });
        fixture.Context.QuestionTopics.Add(new QuestionTopicReadModel { QuestionTopicId = $"{id}-topic", QuestionId = id, TagId = "topic", IsPrimary = true });
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel { VersionId = $"{id}-v", QuestionId = id, VersionNumber = 1, SnapshotSchemaVersion = 2, AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(id, "SingleChoice", "d-1", 12, 1m, [new QuestionTopicSnapshot("topic", true)], [new QuestionAnswerSnapshot($"{id}-answer", "A", true)], [], "content", "solution")), CreatedTime = DateTime.UtcNow });
        fixture.Context.Answers.Add(new AnswerReadModel { AnswerId = $"{id}-answer", QuestionId = id, IsCorrect = true });
    }

    private sealed class NoOpRandomizer : IGenerationRandomizer { public void Shuffle<T>(IList<T> values) { } }
}

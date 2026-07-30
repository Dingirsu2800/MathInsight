using System.Text.Json;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Questions;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class QuestionCandidateCatalogTests
{
    [Fact]
    public async Task Catalog_ReturnsLatestValidV2Candidate_WithAllTopicIds()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddQuestion(testContext, "question-1", ["topic-a", "topic-b"]);
        testContext.Context.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = "question-1-version-2",
            QuestionId = "question-1",
            VersionNumber = 2,
            SnapshotSchemaVersion = 2,
            AnswersSnapshot = CreateSnapshot("question-1", ["topic-a", "topic-b"]),
            CreatedTime = DateTime.UtcNow
        });
        await testContext.Context.SaveChangesAsync();

        var result = await new QuestionCandidateCatalog(testContext.Context).GetCandidatesAsync(
            new QuestionCandidateCatalogFilter(12, ["topic-a"]),
            CancellationToken.None);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("question-1-version-2", candidate.QuestionVersionId);
        Assert.Equal(1.5m, candidate.DefaultWeight);
        Assert.Equal("difficulty-2", candidate.DifficultyId);
        Assert.Equal("SingleChoice", candidate.QuestionType);
        Assert.Equal(["topic-a", "topic-b"], candidate.TagIds.OrderBy(id => id));
        Assert.Contains("AllOrNothing", candidate.SupportedScoringRules);
        Assert.Equal(0, candidate.PartCount);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task Catalog_RejectsMissingOrMalformedLatestSnapshot(string mode)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddQuestion(testContext, "question-1", ["topic-a"]);
        var version = testContext.Context.QuestionVersions.Local.Single(item => item.QuestionId == "question-1");
        if (mode == "missing")
            testContext.Context.QuestionVersions.Remove(version);
        else
            version.AnswersSnapshot = "not-json";

        await testContext.Context.SaveChangesAsync();

        var result = await new QuestionCandidateCatalog(testContext.Context).GetCandidatesAsync(
            new QuestionCandidateCatalogFilter(12, ["topic-a"]),
            CancellationToken.None);

        Assert.Empty(result.Candidates);
        var invalid = Assert.Single(result.InvalidVersionCandidates);
        Assert.Equal("question-1", invalid.QuestionId);
    }

    [Fact]
    public async Task Catalog_RejectsArchivedAnswerOrPartShapeMismatch()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddQuestion(testContext, "question-1", ["topic-a"]);
        var answer = testContext.Context.Answers.Local.Single(item => item.QuestionId == "question-1");
        answer.IsArchived = true;
        await testContext.Context.SaveChangesAsync();

        var result = await new QuestionCandidateCatalog(testContext.Context).GetCandidatesAsync(
            new QuestionCandidateCatalogFilter(12, ["topic-a"]),
            CancellationToken.None);

        Assert.Empty(result.Candidates);
        Assert.Empty(result.InvalidVersionCandidates);
    }

    [Fact]
    public async Task Catalog_AppliesGradeTopicDifficultyAndQuestionTypeFilters()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        AddQuestion(testContext, "valid", ["topic-a"]);
        AddQuestion(testContext, "wrong-grade", ["topic-a"], grade: 11);
        AddQuestion(testContext, "wrong-topic", ["topic-b"]);
        AddQuestion(testContext, "wrong-difficulty", ["topic-a"], difficultyId: "difficulty-1");
        AddQuestion(testContext, "wrong-type", ["topic-a"], questionType: "ShortAnswer");
        await testContext.Context.SaveChangesAsync();

        var result = await new QuestionCandidateCatalog(testContext.Context).GetCandidatesAsync(
            new QuestionCandidateCatalogFilter(12, ["topic-a"], ["difficulty-2"], ["SingleChoice"]),
            CancellationToken.None);

        Assert.Equal(["valid"], result.Candidates.Select(item => item.QuestionId));
    }

    private static void AddQuestion(
        TestGenInMemoryContext testContext,
        string questionId,
        IReadOnlyList<string> tagIds,
        int grade = 12,
        string difficultyId = "difficulty-2",
        string questionType = "SingleChoice")
    {
        testContext.Context.Questions.Add(new QuestionReadModel
        {
            QuestionId = questionId,
            Grade = grade,
            DifficultyId = difficultyId,
            QuestionType = questionType,
            Status = "Approved",
            IsActive = true,
            DefaultWeight = 1.5m
        });
        testContext.Context.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = $"{questionId}-version-1",
            QuestionId = questionId,
            VersionNumber = 1,
            SnapshotSchemaVersion = 2,
            AnswersSnapshot = CreateSnapshot(questionId, tagIds, grade, difficultyId, questionType),
            CreatedTime = DateTime.UtcNow
        });
        foreach (var (tagId, index) in tagIds.Select((tagId, index) => (tagId, index)))
        {
            testContext.Context.QuestionTopics.Add(new QuestionTopicReadModel
            {
                QuestionTopicId = $"{questionId}-topic-{index}",
                QuestionId = questionId,
                TagId = tagId,
                IsPrimary = index == 0
            });
        }

        testContext.Context.Answers.Add(new AnswerReadModel
        {
            AnswerId = $"{questionId}-answer",
            QuestionId = questionId,
            IsCorrect = true,
            IsArchived = false
        });
    }

    private static string CreateSnapshot(
        string questionId,
        IReadOnlyList<string> tagIds,
        int grade = 12,
        string difficultyId = "difficulty-2",
        string questionType = "SingleChoice")
        => JsonSerializer.Serialize(new QuestionSnapshotV2(
            questionId,
            questionType,
            difficultyId,
            grade,
            1.5m,
            tagIds.Select((tagId, index) => new QuestionTopicSnapshot(tagId, index == 0)).ToList(),
            [new QuestionAnswerSnapshot($"{questionId}-answer", "A", true)],
            [],
            "Question content",
            "Solution content"));
}

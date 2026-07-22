using System.Text.Json;
using MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Shared.Questions;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionVersionSnapshotTests
{
    [Theory]
    [InlineData("Approved")]
    [InlineData("Reported")]
    public async Task UpdateQuestion_WhenApprovedOrReported_CreatesVersionSnapshot(string status)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, $"version-{status}", status);

        var result = await new UpdateQuestionCommandHandler(database.Context)
            .Handle(new UpdateQuestionCommand(question.QuestionId, CreateRequest(question), question.ExpertId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.VersionCreated);
        var version = await database.Context.QuestionVersions.SingleAsync();
        Assert.Equal("Updated question content", version.QuestionContent);
        Assert.Equal(question.QuestionId, version.QuestionId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(2, version.SnapshotSchemaVersion);
        var snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(version.AnswersSnapshot);
        Assert.NotNull(snapshot);
        Assert.Equal("Updated question content", snapshot.QuestionContent);
        Assert.Equal("Updated solution content", snapshot.SolutionContent);
        Assert.Equal(version.PictureUrl, snapshot.PictureUrl);
    }

    private static UpdateQuestionRequest CreateRequest(Question question)
    {
        return new UpdateQuestionRequest
        {
            QuestionContent = "Updated question content",
            SolutionContent = "Updated solution content",
            DifficultyId = question.DifficultyId,
            Grade = question.Grade,
            QuestionType = "SINGLE_CHOICE",
            DefaultWeight = 1m,
            Topics = [new CreateQuestionTopicRequest("topic-1", true)],
            Answers =
            [
                new CreateAnswerRequest { AnswerContent = "A", IsCorrect = true },
                new CreateAnswerRequest { AnswerContent = "B", IsCorrect = false }
            ]
        };
    }

    private static async Task<Question> AddQuestionAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string status)
    {
        var difficulty = new TagDifficulty
        {
            DifficultyId = "difficulty-1",
            DifficultyName = "Easy",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        };
        var topic = new TagTopic
        {
            TagId = "topic-1",
            TagName = "Topic",
            Grade = 10,
            DisplayOrder = 1,
            IsActive = true
        };
        var question = new Question
        {
            QuestionId = questionId,
            QuestionContent = "Question content",
            SolutionContent = "Solution content",
            DifficultyId = difficulty.DifficultyId,
            Difficulty = difficulty,
            Grade = 10,
            Status = status,
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true,
            Answers =
            [
                new Answer { AnswerId = "answer-1", AnswerContent = "A", IsCorrect = true },
                new Answer { AnswerId = "answer-2", AnswerContent = "B", IsCorrect = false }
            ],
            QuestionTopics =
            [
                new QuestionTopic
                {
                    QuestionTopicId = "question-topic-1",
                    TagId = topic.TagId,
                    Tag = topic,
                    IsPrimary = true
                }
            ]
        };

        database.Context.Questions.Add(question);
        await database.Context.SaveChangesAsync();
        return question;
    }
}

using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionList;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class GetQuestionListQueryTests
{
    [Fact]
    public async Task Search_TruncatesInputToTwoHundredCharacters()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var searchText = new string('x', 200);
        await AddQuestionAsync(database, "question-1", searchText);

        var result = await new GetQuestionListQueryHandler(database.Context)
            .Handle(
                new GetQuestionListQuery(1, 20, null, null, null, null, null, null, searchText + "extra"),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
    }

    [Theory]
    [InlineData("100%", "100[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[topic]", "[[]topic]")]
    public void Search_EscapesSqlLikeWildcardCharacters(string input, string expectedPattern)
    {
        var pattern = GetQuestionListQueryHandler.EscapeLikePattern(input);

        Assert.Equal(expectedPattern, pattern);
    }

    private static async Task AddQuestionAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string questionContent)
    {
        var difficulty = new TagDifficulty
        {
            DifficultyId = $"difficulty-{questionId}",
            DifficultyName = "Nhận biết",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        };

        var topic = new TagTopic
        {
            TagId = $"topic-{questionId}",
            TagName = "Hàm số",
            Grade = 12,
            DisplayOrder = 1,
            IsActive = true
        };

        database.Context.Questions.Add(new Question
        {
            QuestionId = questionId,
            QuestionContent = questionContent,
            SolutionContent = "Lời giải",
            DifficultyId = difficulty.DifficultyId,
            Difficulty = difficulty,
            Grade = 12,
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = true,
            QuestionTopics =
            [
                new QuestionTopic
                {
                    QuestionTopicId = $"question-topic-{questionId}",
                    TagId = topic.TagId,
                    Tag = topic,
                    IsPrimary = true
                }
            ]
        });

        await database.Context.SaveChangesAsync();
    }
}
